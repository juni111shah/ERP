using ERP.Application.Common;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class ListAttendanceHandler(IAttendanceRecordRepository records, IEmployeeRepository employees, LeaveFeed leaveFeed) : IQueryHandler<ListAttendanceQuery, IReadOnlyList<AttendanceRowDto>>
{
    public async Task<IReadOnlyList<AttendanceRowDto>> Handle(ListAttendanceQuery query, CancellationToken ct)
    {
        // 1. Who is on the payroll in the month, what is recorded and their approved leave.
        var q = query;
        var start = new DateOnly(q.Year, q.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var staff = await employees.ListPayableInAsync(q.LegalEntityId, null, start, end, ct);
        var ids = staff.Select(s => s.Id).ToList();
        var recorded = await records.MapForMonthAsync(q.Year, q.Month, ids, ct);
        var leave = await leaveFeed.MonthAsync(q.Year, q.Month, ids, ct);

        // 2. One row per employee: the record, or full attendance.
        return staff.Select(s =>
        {
            var (paidD, unpaidD) = leave.GetValueOrDefault(s.Id); int paid = (int)Math.Round(paidD), unpaid = (int)Math.Round(unpaidD);
            return recorded.TryGetValue(s.Id, out var r)
                ? new AttendanceRowDto(s.Id, s.Number, s.FullName, s.Designation, s.LegalEntityId, r.Id, true, r.WorkingDays, r.DaysPresent, r.UnpaidAbsentDays, r.PaidLeaveDays, r.OvertimeHours, paid, unpaid)
                : new AttendanceRowDto(s.Id, s.Number, s.FullName, s.Designation, s.LegalEntityId, null, false, 26, 26, 0, 0, 0, paid, unpaid);
        }).ToList();
    }
}
