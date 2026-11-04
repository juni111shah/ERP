using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>NoticeGivenOn is when the employee gave (or was given) notice; with it the unserved part of the notice period can be deducted (setting <c>hr.noticeShortfallDeduction</c>). Without it the notice counts as served.</summary>
public sealed record TerminateInput(DateOnly TerminationDate, string? Reason, DateOnly? LastWorkingDay = null, DateOnly? NoticeGivenOn = null);

/// <summary>What the leaver is owed. Salary is the pro-rated final month (0 when a regular run of that month already paid it); the loan recovery is capped at what is left of the pay.</summary>
public sealed record SettlementPreviewDto(
    Guid EmployeeId, string EmployeeName, DateOnly TerminationDate, DateOnly? LastWorkingDay, int Year, int Month,
    decimal FinalBasic, decimal FinalAllowances, bool SalaryAlreadyPaid, decimal LeaveDays, decimal LeaveEncashment, decimal EndOfService, string? GratuityRule,
    decimal RecurringDeductions, decimal NoticeShortfallDays, decimal NoticeShortfallDeduction, decimal LoanBalance, decimal LoanRecovery, decimal UnrecoveredLoan,
    decimal Gross, decimal Deductions, decimal NetPay, Guid? SettlementRunId, IReadOnlyList<string> Notes);

public sealed record GetSettlementPreview(Guid EmployeeId, DateOnly TerminationDate, DateOnly? NoticeGivenOn) : IQuery<SettlementPreviewDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
/// <summary>Ends the employment: status Terminated, date and reason stored, future-dated leave cancelled. Returns the settlement preview.</summary>
public sealed record TerminateEmployee(Guid EmployeeId, TerminateInput Input) : ICommand<SettlementPreviewDto>, IRequirePermission { public string RequiredPermission => P.EmployeesManage; }
/// <summary>Creates the final-settlement payroll run (kind FinalSettlement, one line) for a terminated employee; it is then posted and paid like any run.</summary>
public sealed record CreateFinalSettlement(Guid EmployeeId) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }

public sealed class TerminateInputValidator : AbstractValidator<TerminateInput>
{
    public TerminateInputValidator()
    {
        RuleFor(x => x.TerminationDate).MustBePlausibleDate();
        RuleFor(x => x.Reason).MaximumLength(300);
        RuleFor(x => x.LastWorkingDay!.Value).LessThanOrEqualTo(x => x.TerminationDate).When(x => x.LastWorkingDay is not null).WithName("LastWorkingDay").WithMessage("The last working day cannot be after the termination date.");
    }
}
public sealed class TerminateEmployeeValidator : AbstractValidator<TerminateEmployee> { public TerminateEmployeeValidator() => RuleFor(x => x.Input).SetValidator(new TerminateInputValidator()); }

/// <summary>Pure settlement arithmetic, kept apart from the database so it can be tested on its own.</summary>
public static class SettlementMath
{
    public static decimal DailyRate(decimal basic, int daysBasis) => daysBasis <= 0 ? 0m : basic / daysBasis;

    /// <summary>Unused leave to pay: what the employee has earned so far this year (all of it, or month by month for monthly accrual) plus carried days, less days taken.</summary>
    public static decimal EncashableDays(decimal entitled, decimal carried, decimal taken, bool monthlyAccrual, int terminationMonth)
    {
        var earned = monthlyAccrual ? LeaveEntitlement.Accrued(entitled, terminationMonth) : entitled;
        return Math.Max(0m, earned + carried - taken);
    }

    /// <summary>Days of the notice period that were not served between the day notice was given and the termination date.</summary>
    public static int NoticeShortfallDays(int? noticePeriodDays, DateOnly? noticeGivenOn, DateOnly terminationDate)
    {
        if (noticePeriodDays is not > 0 || noticeGivenOn is not { } given) return 0;
        var served = Math.Max(0, terminationDate.DayNumber - given.DayNumber);
        return Math.Max(0, noticePeriodDays.Value - served);
    }

    /// <summary>The loan balance is recovered from what is left of the pay, never more than the balance.</summary>
    public static decimal LoanRecovery(decimal loanBalance, decimal payBeforeLoans) => Math.Max(0m, Math.Min(loanBalance, payBeforeLoans));
}

internal sealed class TerminationHandlers(
    IRepository<Employee> employees, IReadRepository<LegalEntity> legalEntities, IReadRepository<LeaveType> leaveTypes, IRepository<LeaveBalance> balances, IRepository<LeaveRequest> leaveRequests,
    IReadRepository<EmployeeLoan> loans, IRepository<PayrollRun> runs, IRepository<PayrollLoanRecovery> recoveries, LeavePolicyService policy, GratuityCalculator gratuity, ISettingsService settings,
    ILegalEntityGuard guard, INumberingService numbering, PayrollSeparation separation, EmployeeHistoryRecorder history, IUnitOfWork uow) :
    IRequestHandler<GetSettlementPreview, SettlementPreviewDto>,
    IRequestHandler<TerminateEmployee, SettlementPreviewDto>,
    IRequestHandler<CreateFinalSettlement, PayrollDto>
{
    public async Task<SettlementPreviewDto> Handle(GetSettlementPreview q, CancellationToken ct)
    {
        var e = await employees.FindAsync(q.EmployeeId, ct) ?? throw new NotFoundException();
        if (q.TerminationDate < e.JoinDate) throw new FieldValidationException("terminationDate", "The termination date cannot be before the join date.");
        return await BuildAsync(e, q.TerminationDate, e.LastWorkingDay, q.NoticeGivenOn ?? e.NoticeGivenOn, ct);
    }

    public Task<SettlementPreviewDto> Handle(TerminateEmployee cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var e = await employees.GetForUpdateAsync(cmd.EmployeeId, token);
        await guard.RequireAsync(e.LegalEntityId, token);
        if (e.Status == EmployeeStatus.Terminated && e.TerminationDate is not null) throw new ConflictException("This employee has already left.");
        if (i.TerminationDate < e.JoinDate) throw new FieldValidationException("terminationDate", "The termination date cannot be before the join date.");
        e.Status = EmployeeStatus.Terminated; e.TerminationDate = i.TerminationDate; e.TerminationReason = i.Reason?.Trim(); e.LastWorkingDay = i.LastWorkingDay; e.NoticeGivenOn = i.NoticeGivenOn;
        history.Terminated(e, i.TerminationDate, i.Reason);
        await CancelFutureLeaveAsync(e, i.TerminationDate, token);
        await employees.SaveChangesAsync(token);
        return await BuildAsync(e, i.TerminationDate, i.LastWorkingDay, i.NoticeGivenOn, token);
    }, ct);

    public Task<PayrollDto> Handle(CreateFinalSettlement cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var e = await employees.FindAsync(cmd.EmployeeId, token) ?? throw new NotFoundException();
        if (e.Status != EmployeeStatus.Terminated || e.TerminationDate is not { } term) throw new ConflictException("Terminate the employee before preparing the final settlement.");
        var entity = await guard.RequireAsync(e.LegalEntityId, token);
        var existing = await ExistingSettlementAsync(e.Id, token);
        if (existing is not null) throw new ConflictException("This employee already has a final settlement. Void it first to prepare a new one.");
        var p = await BuildAsync(e, term, e.LastWorkingDay, e.NoticeGivenOn, token);

        var run = new PayrollRun { LegalEntityId = entity.Id, Year = p.Year, Month = p.Month, Status = PayrollStatus.Draft, Kind = PayrollKind.FinalSettlement, IncludeSalary = true, Notes = $"Final settlement: {e.FullName}" };
        separation.StampGenerated(run);
        run.Number = await numbering.NextAsync(Docs.Payroll, entity.Id, new DateOnly(p.Year, p.Month, 1), token);
        var line = new PayrollLine
        {
            PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, ProratedDays = PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, p.Year, p.Month),
            Basic = p.FinalBasic, Allowances = p.FinalAllowances, LeaveEncashment = p.LeaveEncashment, EndOfService = p.EndOfService, LoanRecovery = p.LoanRecovery,
            GrossEarnings = p.Gross, Deductions = p.Deductions, NetPay = p.NetPay,
            AdjustmentNote = p.NoticeShortfallDeduction > 0 ? $"Includes notice shortfall of {p.NoticeShortfallDays:0} days" : null,
        };
        run.Lines.Add(line);
        run.Employees = 1; run.GrossEarnings = line.GrossEarnings; run.Deductions = line.Deductions; run.NetPay = line.NetPay;
        runs.Add(run);

        // spread the loan recovery over the open loans, oldest first
        var left = p.LoanRecovery;
        foreach (var loan in (await loans.Query(new Specification<EmployeeLoan>(l => l.EmployeeId == e.Id && l.Status == LoanStatus.Active && l.Remaining > 0)).ToListAsync(token)).OrderBy(l => l.StartYear).ThenBy(l => l.StartMonth))
        {
            if (left <= 0) break;
            var take = Math.Min(left, loan.Remaining);
            recoveries.Add(new PayrollLoanRecovery { PayrollRunId = run.Id, PayrollLineId = line.Id, LoanId = loan.Id, Amount = take });
            left -= take;
        }
        await runs.SaveChangesAsync(token);
        return PayrollHandlers.ToDto(run);
    }, ct);

    private async Task<PayrollRun?> ExistingSettlementAsync(Guid employeeId, CancellationToken ct)
        => (await runs.Query(new Specification<PayrollRun>(r => r.Kind == PayrollKind.FinalSettlement && r.Status != PayrollStatus.Voided).Include(r => r.Lines)).ToListAsync(ct)).FirstOrDefault(r => r.Lines.Any(l => l.EmployeeId == employeeId));

    private async Task CancelFutureLeaveAsync(Employee e, DateOnly termination, CancellationToken ct)
    {
        var id = e.Id;
        var open = await leaveRequests.Tracked(new Specification<LeaveRequest>(r => r.EmployeeId == id && r.FromDate > termination
            && (r.Status == LeaveStatus.Draft || r.Status == LeaveStatus.Submitted || r.Status == LeaveStatus.Approved))).ToListAsync(ct);
        foreach (var r in open)
        {
            if (r.Status == LeaveStatus.Approved)
            {
                var typeId = r.LeaveTypeId;
                foreach (var (year, days) in await policy.StoredYearDaysAsync(r, ct))
                {
                    var b = await balances.Tracked(new Specification<LeaveBalance>(x => x.EmployeeId == id && x.LeaveTypeId == typeId && x.Year == year)).FirstOrDefaultAsync(ct);
                    if (b is not null) b.Taken = Math.Max(0, b.Taken - days);
                }
            }
            r.Status = LeaveStatus.Cancelled;
        }
    }

    private async Task<SettlementPreviewDto> BuildAsync(Employee e, DateOnly term, DateOnly? lastWorking, DateOnly? noticeGiven, CancellationToken ct)
    {
        var notes = new List<string>();
        var year = term.Year; var month = term.Month; var monthDays = DateTime.DaysInMonth(year, month);
        var daysBasis = await settings.GetAsync<int?>("hr.daysBasis", e.LegalEntityId, ct) ?? 30;

        // final-month salary, unless a regular run of that month already covers the person
        var employed = PayrollMath.EmployedDays(e.JoinDate, term, year, month);
        var paid = (await runs.Query(new Specification<PayrollRun>(r => r.LegalEntityId == e.LegalEntityId && r.Year == year && r.Month == month && r.Kind == PayrollKind.Regular && r.Status != PayrollStatus.Voided).Include(r => r.Lines)).ToListAsync(ct))
            .Any(r => r.Lines.Any(l => l.EmployeeId == e.Id));
        decimal basic = 0, allowances = 0, recurring = 0;
        if (paid) notes.Add("The regular payroll of this month already includes this employee, so no salary is added here.");
        else if (employed > 0)
        {
            basic = employed < monthDays ? PayrollMath.R(e.BasicSalary * employed / monthDays) : e.BasicSalary;
            allowances = employed < monthDays ? PayrollMath.R(e.Allowances * employed / monthDays) : e.Allowances;
            recurring = e.RecurringDeductions;
        }

        // unused leave of the encashable types
        var daily = SettlementMath.DailyRate(e.BasicSalary, daysBasis);
        decimal leaveDays = 0;
        foreach (var type in await leaveTypes.Query(new Specification<LeaveType>(t => t.Encashable && t.IsActive)).ToListAsync(ct))
        {
            var typeId = type.Id; var empId = e.Id;
            var b = await balances.Query(new Specification<LeaveBalance>(x => x.EmployeeId == empId && x.LeaveTypeId == typeId && x.Year == year)).FirstOrDefaultAsync(ct);
            var entitled = b?.Entitled ?? await policy.EntitlementAsync(e, type, year, ct);
            leaveDays += SettlementMath.EncashableDays(entitled, b?.Carried ?? 0m, b?.Taken ?? 0m, type.AccrualMode == LeaveAccrualMode.Monthly, month);
        }
        var encashment = PayrollMath.R(leaveDays * daily);

        // end of service under the entity's country rule
        var country = await legalEntities.Query(new Specification<LegalEntity>(l => l.Id == e.LegalEntityId)).Select(l => l.CountryCode).FirstOrDefaultAsync(ct) ?? "";
        var g = gratuity.Calculate(e.BasicSalary, e.JoinDate, term, term, country);
        var eos = g.Eligible ? PayrollMath.R(g.Amount) : 0m;

        // unserved notice
        var shortfallDays = SettlementMath.NoticeShortfallDays(e.NoticePeriodDays, noticeGiven, term);
        var shortfall = 0m;
        if (shortfallDays > 0 && await settings.GetAsync<bool?>("hr.noticeShortfallDeduction", e.LegalEntityId, ct) == true) shortfall = PayrollMath.R(shortfallDays * daily);
        else if (shortfallDays > 0) notes.Add($"{shortfallDays} notice days were not served; they are not deducted (setting off).");

        // loans
        var empIdForLoans = e.Id;
        var loanBalance = (await loans.Query(new Specification<EmployeeLoan>(l => l.EmployeeId == empIdForLoans && l.Status == LoanStatus.Active && l.Remaining > 0)).Select(l => l.Remaining).ToListAsync(ct)).Sum();

        var gross = basic + allowances + encashment + eos;
        var before = gross - recurring - shortfall;
        var loanRecovery = SettlementMath.LoanRecovery(loanBalance, before);
        var deductions = recurring + shortfall + loanRecovery;
        var existing = await ExistingSettlementAsync(e.Id, ct);
        return new SettlementPreviewDto(e.Id, e.FullName, term, lastWorking, year, month, basic, allowances, paid, leaveDays, encashment, eos, g.Rule, recurring, shortfallDays, shortfall,
            loanBalance, loanRecovery, loanBalance - loanRecovery, gross, deductions, gross - deductions, existing?.Id, notes);
    }
}
