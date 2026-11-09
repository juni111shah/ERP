using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Approved leave turned into attendance days for a month: paid leave and unpaid leave, in working days. Payroll adds the unpaid days to the recorded absences.</summary>
public sealed class LeaveFeed(IReadRepository<LeaveRequest> requests, IReadRepository<LeaveType> types, LeaveCalendarProvider calendars, LeavePolicyService policy)
{
    /// <summary>Per employee: paid days and "unpaid-equivalent" days of approved leave in the month. Unpaid types count whole days as unpaid; banded types count each leave day by its band (50% pay = half an unpaid day).</summary>
    public async Task<Dictionary<Guid, (decimal Paid, decimal Unpaid)>> MonthAsync(int year, int month, IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, (decimal Paid, decimal Unpaid)>();
        if (employeeIds.Count == 0) return result;
        var start = new DateOnly(year, month, 1); var end = start.AddMonths(1).AddDays(-1);
        var approved = await requests.Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Approved && r.FromDate <= end && r.ToDate >= start && employeeIds.Contains(r.EmployeeId))).ToListAsync(ct);
        if (approved.Count == 0) return result;
        var typeRows = await types.Query().ToDictionaryAsync(t => t.Id, t => t.IsPaid, ct);
        var bandsByType = await policy.BandsByTypeAsync(ct);
        var calendar = await calendars.LoadAsync(start, end, ct);
        foreach (var r in approved.Where(r => !bandsByType.ContainsKey(r.LeaveTypeId)))
        {
            var days = calendar.Count(r.FromDate > start ? r.FromDate : start, r.ToDate < end ? r.ToDate : end);
            var (p, u) = result.GetValueOrDefault(r.EmployeeId);
            result[r.EmployeeId] = typeRows.GetValueOrDefault(r.LeaveTypeId, true) ? (p + days, u) : (p, u + days);
        }

        // banded types: walk the year's approved days of the type in order, so day 1 .. N of the year fall into the right band
        var yearStart = new DateOnly(year, 1, 1);
        foreach (var banded in approved.Where(r => bandsByType.ContainsKey(r.LeaveTypeId)).Select(r => (r.EmployeeId, r.LeaveTypeId)).Distinct())
        {
            var (empId, typeId) = banded;
            var yearRequests = await requests.Query(new Specification<LeaveRequest>(r => r.Status == LeaveStatus.Approved && r.EmployeeId == empId && r.LeaveTypeId == typeId && r.FromDate <= end && r.ToDate >= yearStart))
                .OrderBy(r => r.FromDate).ToListAsync(ct);
            var yearCalendar = await calendars.LoadAsync(yearStart, end, ct);
            var bands = bandsByType[typeId];
            var counted = 0;
            var (p, u) = result.GetValueOrDefault(empId);
            foreach (var r in yearRequests)
                for (var d = r.FromDate < yearStart ? yearStart : r.FromDate; d <= r.ToDate && d <= end; d = d.AddDays(1))
                {
                    if (!yearCalendar.IsWorkingDay(d)) continue;
                    counted++;
                    if (d < start) continue;
                    var paidShare = LeavePayBands.PaidFraction(bands, counted);
                    p += paidShare; u += 1m - paidShare;
                }
            result[empId] = (p, u);
        }
        return result;
    }
}
