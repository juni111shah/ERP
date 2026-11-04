using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Kind OffCycle needs a note; EmployeeIds (optional) limits an off-cycle run; IncludeSalary adds basic, allowances and deductions to the off-cycle lines (otherwise they start at zero for AdjustPayrollLine).</summary>
public sealed record GeneratePayrollInput(Guid LegalEntityId, int Year, int Month, DateOnly? PlannedPayDate, string? Notes, PayrollKind Kind = PayrollKind.Regular, IReadOnlyList<Guid>? EmployeeIds = null, bool IncludeSalary = false);
public sealed record PayrollLineDto(
    Guid Id, Guid EmployeeId, string? EmployeeNumber, string EmployeeName, int AbsentDays, decimal OvertimeHours, decimal Basic, decimal Allowances, decimal Overtime, decimal Absence, decimal Adjustment,
    string? AdjustmentNote, decimal GrossEarnings, decimal Deductions, decimal NetPay, int ProratedDays = 0, DateOnly? PaidOn = null, Guid? PaymentJournalId = null);
public sealed record PayrollDto(
    Guid Id, string Number, Guid LegalEntityId, int Year, int Month, DateOnly? PlannedPayDate, PayrollStatus Status, string? Notes, int Employees, decimal GrossEarnings, decimal Deductions, decimal NetPay,
    DateOnly? PaidOn, Guid? ApprovalRequestId, Guid? AccrualJournalId, Guid? PaymentJournalId, IReadOnlyList<PayrollLineDto> Lines, PayrollKind Kind = PayrollKind.Regular, string? VoidReason = null, DateTime? VoidedAt = null);
public sealed record PayrollSummaryDto(Guid Id, string Number, Guid LegalEntityId, int Year, int Month, PayrollStatus Status, int Employees, decimal GrossEarnings, decimal Deductions, decimal NetPay, DateOnly? PaidOn, PayrollKind Kind = PayrollKind.Regular);

public sealed record GetPayroll(Guid Id) : IQuery<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollRead; }
/// <summary>One CSV line per employee: number, name, IBAN and net pay, for the bank upload.</summary>
public sealed record GetPayrollBankFile(Guid Id, bool UnpaidOnly = false) : IQuery<string>, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
/// <summary>UAE Wages Protection System SIF text: one EDR record per employee, then the SCR summary record. Only for posted or paid runs; reports everything that is missing.</summary>
public sealed record GetPayrollWpsFile(Guid Id, bool UnpaidOnly = false) : IQuery<string>, IRequirePermission { public string RequiredPermission => P.PayrollPay; }
public sealed record GeneratePayroll(GeneratePayrollInput Input) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
public sealed record RecalculatePayroll(Guid Id) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
public sealed record AdjustPayrollLine(Guid RunId, Guid LineId, decimal Adjustment, string? Note) : ICommand<PayrollDto>, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
public sealed record DeletePayroll(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.PayrollManage; }
/// <summary>Posts the accrual (Dr salaries, Cr salaries payable and employee advances). When the setting requires approval the run waits for a decision first.</summary>
public sealed record PostPayroll(Guid Id) : ICommand<PayrollDto>, IRetryOnConcurrency, IRequirePermission { public string RequiredPermission => P.PayrollPost; }

public sealed class GeneratePayrollValidator : AbstractValidator<GeneratePayroll>
{
    public GeneratePayrollValidator()
    {
        RuleFor(x => x.Input.LegalEntityId).NotEmpty();
        RuleFor(x => x.Input.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Input.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Input.Notes).MaximumLength(500);
        RuleFor(x => x.Input.Notes).NotEmpty().When(x => x.Input.Kind == PayrollKind.OffCycle).WithMessage("An off-cycle run needs a note saying why.");
        RuleFor(x => x.Input.Kind).Must(k => k is PayrollKind.Regular or PayrollKind.OffCycle).WithMessage("Choose a regular or off-cycle run.");
    }
}
public sealed class AdjustPayrollLineValidator : AbstractValidator<AdjustPayrollLine>
{
    public AdjustPayrollLineValidator()
    {
        RuleFor(x => x.Note).MaximumLength(300);
        RuleFor(x => x.Adjustment).MustBeMoney();
    }
}
internal sealed class PayrollHandlers(
    IRepository<PayrollRun> runs, IRepository<PayrollLine> lineRows, IReadRepository<Employee> employees, IReadRepository<AttendanceRecord> attendance, LeaveFeed leaveFeed, IReadRepository<Account> accounts, IClock clock,
    IReadRepository<ApprovalRequest> approvalRequests, IApprovalService approvals, ISettingsService settings, ILegalEntityGuard guard, IPostingService posting, INumberingService numbering,
    IUnitOfWork uow, PayrollSeparation separation) :
    IRequestHandler<GetPayroll, PayrollDto>,
    IRequestHandler<GetPayrollBankFile, string>,
    IRequestHandler<GetPayrollWpsFile, string>,
    IRequestHandler<GeneratePayroll, PayrollDto>,
    IRequestHandler<RecalculatePayroll, PayrollDto>,
    IRequestHandler<AdjustPayrollLine, PayrollDto>,
    IRequestHandler<DeletePayroll, Unit>,
    IRequestHandler<PostPayroll, PayrollDto>
{
    public async Task<PayrollDto> Handle(GetPayroll q, CancellationToken ct)
        => ToDto(await runs.Query(new Specification<PayrollRun>(r => r.Id == q.Id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException());

    public async Task<string> Handle(GetPayrollBankFile q, CancellationToken ct)
    {
        var run = await runs.Query(new Specification<PayrollRun>(r => r.Id == q.Id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Post the payroll before preparing the bank file.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        var fileLines = q.UnpaidOnly ? run.Lines.Where(l => l.PaidOn is null && run.Status != PayrollStatus.Paid).ToList() : run.Lines;
        var ids = fileLines.Select(l => l.EmployeeId).ToList();
        var ibans = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id))).Select(e => new { e.Id, e.Iban }).ToListAsync(ct)).ToDictionary(e => e.Id, e => e.Iban);
        var sb = new StringBuilder("Employee no.,Name,IBAN,Net pay\n");
        foreach (var l in fileLines.OrderBy(l => l.EmployeeNumber))
            sb.Append(Csv(l.EmployeeNumber)).Append(',').Append(Csv(l.EmployeeName)).Append(',').Append(Csv(ibans.GetValueOrDefault(l.EmployeeId))).Append(',').Append(l.NetPay.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return sb.ToString();
    }

    public async Task<string> Handle(GetPayrollWpsFile q, CancellationToken ct)
    {
        var run = await runs.Query(new Specification<PayrollRun>(r => r.Id == q.Id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();
        if (run.Status is PayrollStatus.Draft or PayrollStatus.PendingApproval) throw new ConflictException("Post the payroll before preparing the WPS file.");
        if (run.Status == PayrollStatus.Voided) throw new ConflictException("This payroll was voided.");
        var fileLines = q.UnpaidOnly ? run.Lines.Where(l => l.PaidOn is null && run.Status != PayrollStatus.Paid).ToList() : run.Lines;
        var ids = fileLines.Select(l => l.EmployeeId).ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(ct)).ToDictionary(e => e.Id);
        var eid = (await settings.GetAsync<string>("hr.employerEstablishmentId", run.LegalEntityId, ct))?.Trim();
        var employerBank = (await settings.GetAsync<string>("hr.employerBankRoutingCode", run.LegalEntityId, ct))?.Trim();

        var missing = new List<string>();
        if (string.IsNullOrEmpty(eid)) missing.Add("the employer establishment ID (setting hr.employerEstablishmentId)");
        if (string.IsNullOrEmpty(employerBank)) missing.Add("the employer bank routing code (setting hr.employerBankRoutingCode)");
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var start = new DateOnly(run.Year, run.Month, 1);
        var end = new DateOnly(run.Year, run.Month, DateTime.DaysInMonth(run.Year, run.Month));
        var sb = new StringBuilder();
        decimal total = 0;
        foreach (var l in fileLines.OrderBy(l => l.EmployeeNumber))
        {
            var e = staff.GetValueOrDefault(l.EmployeeId);
            var who = $"{l.EmployeeNumber} {l.EmployeeName}";
            var card = Clean(e?.LabourCardNumber) ?? Clean(CustomValue(e, "cf_ae_mohre_personal_no"));
            var routing = Clean(e?.BankRoutingCode) ?? Clean(CustomValue(e, "cf_ae_bank_routing_code"));
            var iban = Clean(e?.Iban);
            if (card is null) missing.Add($"{who}: labour card / MOHRE personal number");
            if (routing is null) missing.Add($"{who}: bank routing code");
            if (iban is null) missing.Add($"{who}: IBAN");
            var variable = Math.Max(0m, PayrollMath.R(l.Overtime + l.Adjustment));
            var fixedIncome = PayrollMath.R(l.NetPay) - variable;
            total += l.NetPay;
            sb.Append("EDR,").Append(card).Append(',').Append(routing).Append(',').Append(iban).Append(',')
              .Append(start.ToString("yyyy-MM-dd")).Append(',').Append(end.ToString("yyyy-MM-dd")).Append(',').Append(end.Day).Append(',')
              .Append(fixedIncome.ToString("0.00", invariant)).Append(',').Append(variable.ToString("0.00", invariant)).Append(',').Append(l.AbsentDays).Append('\n');
        }
        if (missing.Count > 0) throw new ConflictException("The WPS file cannot be prepared yet. Missing: " + string.Join("; ", missing) + ".");
        var now = clock.UtcNow;
        sb.Append("SCR,").Append(eid).Append(',').Append(employerBank).Append(',').Append(now.ToString("yyyy-MM-dd")).Append(',').Append(now.ToString("HHmm")).Append(',')
          .Append(run.Month.ToString("00")).Append(run.Year).Append(',').Append(fileLines.Count).Append(',').Append(PayrollMath.R(total).ToString("0.00", invariant)).Append(",AED\n");
        return sb.ToString();
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().Replace(" ", "");

    private static string? CustomValue(Employee? e, string key)
    {
        if (e?.CustomFields is not { Length: > 0 } json) return null;
        try { return System.Text.Json.Nodes.JsonNode.Parse(json)?[key]?.GetValue<string>(); } catch { return null; }
    }

    public Task<PayrollDto> Handle(GeneratePayroll cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, token);
        if (i.Kind == PayrollKind.Regular && await runs.AnyAsync(r => r.LegalEntityId == entity.Id && r.Year == i.Year && r.Month == i.Month && r.Kind == PayrollKind.Regular && r.Status != PayrollStatus.Voided, token))
            throw new ConflictException("There is already a payroll run for this entity and month. Void it first, or create an off-cycle run.");
        var run = new PayrollRun { LegalEntityId = entity.Id, Year = i.Year, Month = i.Month, PlannedPayDate = i.PlannedPayDate, Notes = i.Notes?.Trim(), Status = PayrollStatus.Draft, Kind = i.Kind, IncludeSalary = i.Kind == PayrollKind.Regular || i.IncludeSalary };
        run.Number = await numbering.NextAsync(Docs.Payroll, entity.Id, new DateOnly(i.Year, i.Month, 1), token);
        var lines = await BuildLinesAsync(run, new Dictionary<Guid, (decimal, string?)>(), i.Kind == PayrollKind.OffCycle && i.EmployeeIds is { Count: > 0 } only ? only.ToHashSet() : null, token);
        if (lines.Count == 0) throw new ConflictException("No active employees are on the payroll for this month.");
        run.Lines.AddRange(lines); Totals(run);
        runs.Add(run);
        await runs.SaveChangesAsync(token);
        return ToDto(run);
    }, ct);

    public Task<PayrollDto> Handle(RecalculatePayroll cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var run = await LoadDraftAsync(cmd.Id, token);
        await WithdrawPendingAsync(run, token);
        var keep = run.Lines.Where(l => l.Adjustment != 0).ToDictionary(l => l.EmployeeId, l => (l.Adjustment, l.AdjustmentNote));
        foreach (var old in run.Lines.ToList()) lineRows.Remove(old);
        var fresh = await BuildLinesAsync(run, keep, run.Kind == PayrollKind.OffCycle ? run.Lines.Select(l => l.EmployeeId).ToHashSet() : null, token);
        lineRows.AddRange(fresh);
        run.Lines.Clear(); run.Lines.AddRange(fresh); Totals(run); run.Status = PayrollStatus.Draft; run.ApprovalRequestId = null; separation.StampAdjusted(run);
        await runs.SaveChangesAsync(token);
        return ToDto(run);
    }, ct);

    public async Task<PayrollDto> Handle(AdjustPayrollLine cmd, CancellationToken ct)
    {
        var run = await LoadDraftAsync(cmd.RunId, ct);
        await WithdrawPendingAsync(run, ct);
        var line = run.Lines.FirstOrDefault(l => l.Id == cmd.LineId) ?? throw new NotFoundException();
        var (gross, net) = PayrollMath.WithAdjustment(line.Basic, line.Allowances, line.Overtime, line.Absence, cmd.Adjustment, line.Deductions);
        if (gross < 0 || net < 0) throw new FieldValidationException("adjustment", "The adjustment would make the pay negative.");
        line.Adjustment = cmd.Adjustment; line.AdjustmentNote = cmd.Note?.Trim(); line.GrossEarnings = gross; line.NetPay = net;
        Totals(run); run.Status = PayrollStatus.Draft; run.ApprovalRequestId = null;
        await runs.SaveChangesAsync(ct);
        return ToDto(run);
    }

    public async Task<Unit> Handle(DeletePayroll cmd, CancellationToken ct)
    {
        var run = await LoadDraftAsync(cmd.Id, ct);
        await WithdrawPendingAsync(run, ct);
        runs.Remove(run);
        await runs.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<PayrollDto> Handle(PostPayroll cmd, CancellationToken ct) => uow.InTransactionAsync(token => PostPayrollCoreAsync(cmd, token), ct);

    private async Task<PayrollDto> PostPayrollCoreAsync(PostPayroll cmd, CancellationToken ct)
    {
        var run = await LoadAsync(cmd.Id, ct);
        if (run.Status is not (PayrollStatus.Draft or PayrollStatus.PendingApproval)) throw new ConflictException("Only a draft payroll can be posted.");
        if (run.NetPay <= 0) throw new ConflictException("A payroll with no net pay cannot be posted.");
        await separation.RequireCanPostAsync(run, ct);

        if (await settings.GetAsync<bool?>("hr.payrollRequiresApproval", run.LegalEntityId, ct) == true)
        {
            var request = run.ApprovalRequestId is { } rid ? await approvalRequests.FindAsync(rid, ct) : null;
            if (request is null || request.Status is ApprovalStatus.Rejected or ApprovalStatus.Withdrawn)
            {
                var submitted = await approvals.SubmitAsync("payroll", run.Id, $"Payroll {run.Number} {run.Year}-{run.Month:00}", run.NetPay, null, run.LegalEntityId, ct);
                run.ApprovalRequestId = submitted.Id; run.Status = PayrollStatus.PendingApproval;
                await runs.SaveChangesAsync(ct);
                return ToDto(run);
            }
            if (request.Status == ApprovalStatus.Pending) throw new ConflictException("This payroll is waiting for approval.");
        }

        var expense = await posting.AccountForRoleAsync("payroll.expense", ct);
        var payable = await posting.AccountForRoleAsync("payroll.accrued", ct);
        var memo = $"Payroll accrual {run.Year}-{run.Month:00}";
        var lines = new List<PostingLine> { new(expense, run.GrossEarnings, 0, memo), new(payable, 0, run.NetPay, memo) };
        if (run.Deductions > 0) lines.Add(new PostingLine(await DeductionsAccountAsync(ct), 0, run.Deductions, memo));
        var date = new DateOnly(run.Year, run.Month, DateTime.DaysInMonth(run.Year, run.Month));
        var journal = await posting.PostAsync(new PostingRequest(run.LegalEntityId, date, "payroll", run.Id, memo, run.Number, lines), ct);
        run.AccrualJournalId = journal.Id; run.Status = PayrollStatus.Posted; separation.StampPosted(run);
        await runs.SaveChangesAsync(ct);
        return ToDto(run);
    }

    /// <summary>Recurring deductions credit the "payroll.deductions" account when the workspace has one, otherwise the employee advances account (the original behaviour).</summary>
    private async Task<Guid> DeductionsAccountAsync(CancellationToken ct)
    {
        var id = await accounts.Query(new Specification<Account>(a => a.SystemRole == "payroll.deductions" && a.IsActive)).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        return id ?? await posting.AccountForRoleAsync("payroll.advances", ct);
    }

    private sealed record PaySettings(int DaysBasis, decimal OvertimeMultiplier, int HoursPerDay);

    private async Task<PaySettings> ReadSettingsAsync(Guid entityId, CancellationToken ct)
        => new(await settings.GetAsync<int?>("hr.daysBasis", entityId, ct) ?? 30, await settings.GetAsync<decimal?>("hr.overtimeMultiplier", entityId, ct) ?? 1.25m, await settings.GetAsync<int?>("hr.hoursPerDay", entityId, ct) ?? 8);

    private async Task<List<PayrollLine>> BuildLinesAsync(PayrollRun run, IReadOnlyDictionary<Guid, (decimal Adjustment, string? Note)> adjustments, IReadOnlySet<Guid>? only, CancellationToken ct)
    {
        var start = new DateOnly(run.Year, run.Month, 1); var end = start.AddMonths(1).AddDays(-1); var monthDays = end.Day;
        var all = (await employees.Query(new Specification<Employee>(e => e.LegalEntityId == run.LegalEntityId)).ToListAsync(ct)).Where(e => only is null || only.Contains(e.Id));
        var withSalary = run.Kind == PayrollKind.Regular || run.IncludeSalary;
        // a zero-salary off-cycle line may name anybody of the entity (a bonus for someone who has left); salary lines need the person employed in the month
        var staff = all.Where(e => !withSalary ? only is not null || e.IsPayableIn(start, end) : e.IsPayableIn(start, end) && PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, run.Year, run.Month) > 0).OrderBy(e => e.Number).ToList();
        var ids = staff.Select(s => s.Id).ToList();
        var records = (await attendance.Query(new Specification<AttendanceRecord>(r => r.Year == run.Year && r.Month == run.Month && ids.Contains(r.EmployeeId))).ToListAsync(ct)).ToDictionary(r => r.EmployeeId);
        var s = await ReadSettingsAsync(run.LegalEntityId, ct);
        var leave = await leaveFeed.MonthAsync(run.Year, run.Month, ids, ct);
        return staff.Select(e =>
        {
            var (adj, note) = adjustments.TryGetValue(e.Id, out var a0) ? a0 : (0m, null);
            if (!withSalary)
                return new PayrollLine { PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, Adjustment = adj, AdjustmentNote = note, GrossEarnings = adj, NetPay = adj };
            records.TryGetValue(e.Id, out var rec);
            var absent = (rec?.UnpaidAbsentDays ?? 0) + leave.GetValueOrDefault(e.Id).Unpaid; // recorded absences + approved unpaid leave
            var employed = PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, run.Year, run.Month);
            var a = PayrollMath.Calculate(e.BasicSalary, e.Allowances, e.RecurringDeductions, absent, rec?.OvertimeHours ?? 0, adj, s.DaysBasis, s.OvertimeMultiplier, s.HoursPerDay, employed, monthDays);
            return new PayrollLine
            {
                PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, AbsentDays = absent, OvertimeHours = rec?.OvertimeHours ?? 0, ProratedDays = employed,
                Basic = a.Basic, Allowances = a.Allowances, Overtime = a.Overtime, Absence = a.Absence, Adjustment = adj, AdjustmentNote = note, GrossEarnings = a.GrossEarnings, Deductions = a.Deductions, NetPay = a.NetPay,
            };
        }).ToList();
    }

    private static void Totals(PayrollRun run)
    {
        run.Employees = run.Lines.Count; run.GrossEarnings = run.Lines.Sum(l => l.GrossEarnings); run.Deductions = run.Lines.Sum(l => l.Deductions); run.NetPay = run.Lines.Sum(l => l.NetPay);
    }

    /// <summary>A change invalidates a request still waiting for a decision; a decided one only loses its link when the run is recalculated.</summary>
    private async Task WithdrawPendingAsync(PayrollRun run, CancellationToken ct)
    {
        if (run.ApprovalRequestId is not { } rid) return;
        var request = await approvalRequests.FindAsync(rid, ct);
        if (request?.Status == ApprovalStatus.Pending) await approvals.WithdrawAsync(rid, ct);
    }

    private async Task<PayrollRun> LoadAsync(Guid id, CancellationToken ct)
        => await runs.Tracked(new Specification<PayrollRun>(r => r.Id == id).Include(r => r.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private async Task<PayrollRun> LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var run = await LoadAsync(id, ct);
        if (run.Status is not (PayrollStatus.Draft or PayrollStatus.PendingApproval)) throw new ConflictException("Only a draft payroll can be changed.");
        return run;
    }

    private static string Csv(string? v) => v is null ? "" : $"\"{v.Replace("\"", "\"\"")}\"";

    internal static PayrollDto ToDto(PayrollRun r) => new(r.Id, r.Number, r.LegalEntityId, r.Year, r.Month, r.PlannedPayDate, r.Status, r.Notes, r.Employees, r.GrossEarnings, r.Deductions, r.NetPay, r.PaidOn,
        r.ApprovalRequestId, r.AccrualJournalId, r.PaymentJournalId,
        r.Lines.OrderBy(l => l.EmployeeNumber).Select(l => new PayrollLineDto(l.Id, l.EmployeeId, l.EmployeeNumber, l.EmployeeName, l.AbsentDays, l.OvertimeHours, l.Basic, l.Allowances, l.Overtime, l.Absence, l.Adjustment, l.AdjustmentNote,
            l.GrossEarnings, l.Deductions, l.NetPay, l.ProratedDays, l.PaidOn, l.PaymentJournalId)).ToList(), r.Kind, r.VoidReason, r.VoidedAt);
}
