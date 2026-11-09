using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Works out the lines of a payroll run (pay, absences, overtime, statutory rules, loan instalments); used when a run is generated and recalculated.</summary>
public sealed class PayrollLineBuilder(
    IEmployeeRepository employees, IPayrollRunRepository runs, IAttendanceRecordRepository attendance, LeaveFeed leaveFeed, IEmployeeLoanRepository loanRows, StatutoryEngine statutory, ISettingsService settings)
{
    private sealed record PaySettings(int DaysBasis, decimal OvertimeMultiplier, int HoursPerDay);

    private async Task<PaySettings> ReadSettingsAsync(Guid entityId, CancellationToken ct)
        => new(await settings.GetAsync<int?>("hr.daysBasis", entityId, ct) ?? 30, await settings.GetAsync<decimal?>("hr.overtimeMultiplier", entityId, ct) ?? 1.25m, await settings.GetAsync<int?>("hr.hoursPerDay", entityId, ct) ?? 8);

    public async Task<(List<PayrollLine> Lines, List<PayrollLoanRecovery> Recoveries, List<PayrollLineStatutory> Statutory)> BuildLinesAsync(PayrollRun run, IReadOnlyDictionary<Guid, (decimal Adjustment, string? Note)> adjustments, IReadOnlySet<Guid>? only, CancellationToken ct)
    {
        var start = new DateOnly(run.Year, run.Month, 1); var end = start.AddMonths(1).AddDays(-1); var monthDays = end.Day;
        var all = (await employees.ListOfEntityAsync(run.LegalEntityId, ct)).Where(e => only is null || only.Contains(e.Id));
        var withSalary = run.Kind == PayrollKind.Regular || run.IncludeSalary;
        // a zero-salary off-cycle line may name anybody of the entity (a bonus for someone who has left); salary lines need the person employed in the month
        var staff = all.Where(e => !withSalary ? only is not null || e.IsPayableIn(start, end) : e.IsPayableIn(start, end) && PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, run.Year, run.Month) > 0).OrderBy(e => e.Number).ToList();
        if (run.Kind == PayrollKind.Regular)
        {
            // someone whose final settlement already paid this month's salary is not on the regular run
            var settledRuns = await runs.ListLiveFinalSettlementsWithLinesAsync(run.LegalEntityId, run.Year, run.Month, ct);
            var settled = settledRuns.SelectMany(r => r.Lines).Where(l => l.Basic > 0 || l.Allowances > 0).Select(l => l.EmployeeId).ToHashSet();
            if (settled.Count > 0) staff = staff.Where(e => !settled.Contains(e.Id)).ToList();
        }
        var ids = staff.Select(s => s.Id).ToList();
        var records = await attendance.MapForMonthAsync(run.Year, run.Month, ids, ct);
        var s = await ReadSettingsAsync(run.LegalEntityId, ct);
        var leave = await leaveFeed.MonthAsync(run.Year, run.Month, ids, ct);
        var openLoans = run.Kind != PayrollKind.Regular ? new Dictionary<Guid, List<EmployeeLoan>>()
            : (await loanRows.ListRecoverableOfEntityAsync(run.LegalEntityId, ids, ct))
                .Where(l => LoanMath.Started(l, run.Year, run.Month)).GroupBy(l => l.EmployeeId).ToDictionary(g => g.Key, g => g.OrderBy(l => l.StartYear).ThenBy(l => l.StartMonth).ToList());
        var recs = new List<PayrollLoanRecovery>();
        var stats = new List<PayrollLineStatutory>();
        var statutoryRules = run.Kind == PayrollKind.Regular ? await statutory.LoadAsync(run.LegalEntityId, run.Year, run.Month, ct) : [];
        var built = staff.Select(e =>
        {
            var (adj, note) = adjustments.TryGetValue(e.Id, out var a0) ? a0 : (0m, null);
            if (!withSalary)
                return new PayrollLine { PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, Adjustment = adj, AdjustmentNote = note, GrossEarnings = adj, NetPay = adj };
            records.TryGetValue(e.Id, out var rec);
            var absentExact = (rec?.UnpaidAbsentDays ?? 0) + leave.GetValueOrDefault(e.Id).Unpaid; // recorded absences + approved unpaid leave (banded sick leave counts as a fraction)
            var absent = (int)Math.Round(absentExact, MidpointRounding.AwayFromZero);
            var employed = PayrollMath.EmployedDays(e.JoinDate, e.TerminationDate, run.Year, run.Month);
            var a = PayrollMath.Calculate(e.BasicSalary, e.Allowances, e.RecurringDeductions, absentExact, rec?.OvertimeHours ?? 0, adj, s.DaysBasis, s.OvertimeMultiplier, s.HoursPerDay, employed, monthDays);
            var line = new PayrollLine
            {
                PayrollRunId = run.Id, EmployeeId = e.Id, EmployeeName = e.FullName, EmployeeNumber = e.Number, AbsentDays = absent, OvertimeHours = rec?.OvertimeHours ?? 0, ProratedDays = employed,
                Basic = a.Basic, Allowances = a.Allowances, Overtime = a.Overtime, Absence = a.Absence, Adjustment = adj, AdjustmentNote = note, GrossEarnings = a.GrossEarnings, Deductions = a.Deductions, NetPay = a.NetPay,
            };
            // statutory rules (social security, tax ...) come out before loan instalments; an employee's share never takes the net pay below zero
            if (statutoryRules.Count > 0)
            {
                var results = StatutoryEngine.For(statutoryRules, e.Nationality, a.Basic, a.GrossEarnings);
                var employeeShare = Math.Min(results.Where(x => x.Kind == StatutoryKind.EmployeeDeduction).Sum(x => x.Amount), Math.Max(0m, line.NetPay));
                line.StatutoryDeduction = employeeShare; line.Deductions += employeeShare; line.NetPay -= employeeShare;
                line.EmployerCost = results.Where(x => x.Kind == StatutoryKind.EmployerCost).Sum(x => x.Amount);
                foreach (var r in results)
                    stats.Add(new PayrollLineStatutory { PayrollRunId = run.Id, PayrollLineId = line.Id, EmployeeId = e.Id, RuleId = r.RuleId, Code = r.Code, Name = r.Name, Kind = r.Kind, BaseAmount = r.BaseAmount, Amount = r.Amount });
            }
            // loan instalments come out last and never take the net pay below zero (the rest waits for the next month); only money actually paid out is recovered
            if (openLoans.TryGetValue(e.Id, out var mine))
                foreach (var loan in mine)
                {
                    var take = LoanMath.Recovery(loan.InstalmentAmount, loan.Remaining, line.NetPay);
                    if (take <= 0) continue;
                    recs.Add(new PayrollLoanRecovery { PayrollRunId = run.Id, PayrollLineId = line.Id, LoanId = loan.Id, Amount = take });
                    line.LoanRecovery += take; line.Deductions += take; line.NetPay -= take;
                }
            return line;
        }).ToList();
        return (built, recs, stats);
    }
}
