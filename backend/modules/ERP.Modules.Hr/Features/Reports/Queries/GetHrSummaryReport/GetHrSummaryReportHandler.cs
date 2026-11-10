using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class GetHrSummaryReportHandler(
    IEmployeeRepository employees, IPayrollRunRepository runs, ILeaveRequestRepository leave, ILeaveTypeRepository leaveTypes, IAttendanceRecordRepository attendance,
    ICurrentUser user, IClock clock) : IQueryHandler<GetHrSummaryReportQuery, HrSummaryReportDto>
{
    public async Task<HrSummaryReportDto> Handle(GetHrSummaryReportQuery query, CancellationToken ct)
    {
        // 1. The period and everyone of the entity; who is employed at its end.
        var q = query;
        var to = q.To ?? DateOnly.FromDateTime(clock.UtcNow);
        var from = q.From ?? new DateOnly(to.Year, 1, 1);
        var entity = q.LegalEntityId;
        var everyone = await employees.ListReportRowsAsync(entity, ct);

        var current = everyone.Where(e => e.JoinDate <= to && (e.TerminationDate is null || e.TerminationDate > to) && !(e.Status == EmployeeStatus.Terminated && e.TerminationDate is null)).ToList();
        static string Label(string? v) => string.IsNullOrWhiteSpace(v) ? "Unspecified" : v.Trim();
        static List<HrCount> Count(IEnumerable<string> keys) => keys.GroupBy(k => k, StringComparer.OrdinalIgnoreCase).Select(g => new HrCount(g.First(), g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Key).ToList();

        // 2. Joiners and leavers per month, average service.
        var months = Months(from, to);
        var movements = months.Select(m => new HrMonthMovement(m.Year, m.Month,
            everyone.Count(e => e.JoinDate.Year == m.Year && e.JoinDate.Month == m.Month),
            everyone.Count(e => e.TerminationDate is { } t && t.Year == m.Year && t.Month == m.Month))).ToList();
        var avg = current.Count == 0 ? 0m : Math.Round(current.Average(e => (decimal)(to.DayNumber - e.JoinDate.DayNumber) / 365.25m), 2);

        // 3. Payroll cost per month, only for callers who may read payroll.
        List<HrMonthCost>? cost = null;
        if (user.HasPermission(P.PayrollRead))
        {
            var lo = from.Year * 12 + from.Month; var hi = to.Year * 12 + to.Month;
            var posted = await runs.ListPostedTotalsAsync(entity, lo, hi, ct);
            cost = months.Select(m =>
            {
                var rows = posted.Where(r => r.Year == m.Year && r.Month == m.Month).ToList();
                return new HrMonthCost(m.Year, m.Month, rows.Count, rows.Sum(r => r.Employees), rows.Sum(r => r.GrossEarnings), rows.Sum(r => r.Deductions), rows.Sum(r => r.NetPay));
            }).ToList();
        }

        // 4. Approved leave by type and unpaid absence per month.
        var approved = await leave.ListApprovedDaysAsync(entity, from, to, ct);
        var typeNames = await leaveTypes.NamesByIdAsync(ct);
        var leaveByType = approved.GroupBy(r => r.LeaveTypeId).Select(g => new HrLeaveByType(typeNames.GetValueOrDefault(g.Key, "Unknown"), g.Sum(r => r.Days))).OrderByDescending(x => x.Days).ThenBy(x => x.LeaveType).ToList();

        var lo2 = from.Year * 12 + from.Month; var hi2 = to.Year * 12 + to.Month;
        var attended = await attendance.ListUnpaidAbsenceAsync(entity, lo2, hi2, ct);
        var absence = months.Select(m => new HrMonthAbsence(m.Year, m.Month, attended.Where(a => a.Year == m.Year && a.Month == m.Month).Sum(a => a.UnpaidAbsentDays))).ToList();

        return new HrSummaryReportDto(from, to, entity, current.Count, avg,
            Count(current.Select(e => Label(e.Department))), Count(current.Select(e => Label(e.Nationality))),
            Count(current.Select(e => Label(e.ContractType?.ToString() ?? EmployeeReportFields.Text(e.CustomFields, EmployeeReportFields.ContractType)))),
            movements, cost, leaveByType, absence.Sum(a => a.Days), absence);
    }

    private static List<(int Year, int Month)> Months(DateOnly from, DateOnly to)
    {
        var list = new List<(int, int)>();
        for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1)) list.Add((m.Year, m.Month));
        return list;
    }
}
