using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Works out what a leaver is owed (the settlement preview) and finds an existing final settlement; shared by the termination handlers.</summary>
public sealed class SettlementCalculator(
    IPayrollRunRepository runs, IReadRepository<LegalEntity> legalEntities, ILeaveTypeRepository leaveTypes, ILeaveBalanceRepository balances, IEmployeeLoanRepository loans,
    LeavePolicyService policy, GratuityCalculator gratuity, ISettingsService settings, StatutoryEngine statutory)
{
    /// <summary>The entity's statutory rules on the final month's salary, the base a regular run of that month would use. No salary here (a regular run already paid it) means nothing to deduct twice; leave encashment and end of service are outside the base.</summary>
    public async Task<IReadOnlyList<StatutoryLineResult>> StatutoryForAsync(Employee e, decimal basic, decimal allowances, int year, int month, CancellationToken ct)
        => basic + allowances <= 0 ? [] : StatutoryEngine.For(await statutory.LoadAsync(e.LegalEntityId, year, month, ct), e.Nationality, basic, basic + allowances);

    public Task<PayrollRun?> ExistingSettlementAsync(Guid employeeId, CancellationToken ct) => runs.FindLiveFinalSettlementOfAsync(employeeId, ct);

    public async Task<SettlementPreviewDto> BuildAsync(Employee e, DateOnly term, DateOnly? lastWorking, DateOnly? noticeGiven, CancellationToken ct)
    {
        var notes = new List<string>();
        var year = term.Year; var month = term.Month; var monthDays = DateTime.DaysInMonth(year, month);
        var daysBasis = await settings.GetAsync<int?>("hr.daysBasis", e.LegalEntityId, ct) ?? 30;

        // final-month salary, unless a regular run of that month already covers the person
        var employed = PayrollMath.EmployedDays(e.JoinDate, term, year, month);
        var paid = await runs.IsOnLiveRegularRunAsync(e.LegalEntityId, year, month, e.Id, ct);
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
        foreach (var type in await leaveTypes.ListActiveEncashableAsync(ct))
        {
            var b = await balances.FindForYearAsync(e.Id, type.Id, year, ct);
            var entitled = b?.Entitled ?? await policy.EntitlementAsync(e, type, year, ct);
            leaveDays += SettlementMath.EncashableDays(entitled, b?.Carried ?? 0m, b?.Taken ?? 0m, type.AccrualMode == LeaveAccrualMode.Monthly, month);
        }
        var encashment = PayrollMath.R(leaveDays * daily);

        // end of service under the entity's country rule
        var country = (await legalEntities.FindAsync(e.LegalEntityId, ct))?.CountryCode ?? "";
        var g = gratuity.Calculate(e.BasicSalary, e.JoinDate, term, term, country, daysBasis);
        var eos = g.Eligible ? PayrollMath.R(g.Amount) : 0m;

        // unserved notice
        var shortfallDays = SettlementMath.NoticeShortfallDays(e.NoticePeriodDays, noticeGiven, term);
        var shortfall = 0m;
        if (shortfallDays > 0 && await settings.GetAsync<bool?>("hr.noticeShortfallDeduction", e.LegalEntityId, ct) == true) shortfall = PayrollMath.R(shortfallDays * daily);
        else if (shortfallDays > 0) notes.Add($"{shortfallDays} notice days were not served; they are not deducted (setting off).");

        // loans
        var openLoans = await loans.ListOpenBalancesAsync(e.Id, ct);
        var loanBalance = openLoans.Where(l => l.DisbursedOn != null).Sum(l => l.Remaining);
        if (openLoans.Any(l => l.DisbursedOn == null)) notes.Add("A loan that was never paid out is not recovered here.");

        var gross = basic + allowances + encashment + eos;
        var before = gross - recurring - shortfall;
        var rules = await StatutoryForAsync(e, basic, allowances, year, month, ct);
        var statutoryDeduction = SettlementMath.StatutoryShare(rules.Where(r => r.Kind == StatutoryKind.EmployeeDeduction).Sum(r => r.Amount), before);
        var employerCost = rules.Where(r => r.Kind == StatutoryKind.EmployerCost).Sum(r => r.Amount);
        if (rules.Count > 0) notes.Add("Statutory deductions are worked out on the final month's salary; leave pay and end of service are outside their base.");
        var loanRecovery = SettlementMath.LoanRecovery(loanBalance, before - statutoryDeduction);
        var deductions = recurring + shortfall + statutoryDeduction + loanRecovery;
        var existing = await ExistingSettlementAsync(e.Id, ct);
        return new SettlementPreviewDto(e.Id, e.FullName, term, lastWorking, year, month, basic, allowances, paid, leaveDays, encashment, eos, g.Rule, recurring, shortfallDays, shortfall,
            loanBalance, loanRecovery, loanBalance - loanRecovery, gross, deductions, gross - deductions, existing?.Id, notes, statutoryDeduction, employerCost);
    }
}
