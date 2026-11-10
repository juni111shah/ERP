using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class RollUpAttendanceHandler(
    IAttendanceLogRepository logs, IAttendanceRecordRepository records, IEmployeeRepository employees, LeaveCalendarProvider calendars, IUnitOfWork uow)
    : ICommandHandler<RollUpAttendanceCommand, IReadOnlyList<AttendanceRollUpRow>>
{
    public Task<IReadOnlyList<AttendanceRollUpRow>> Handle(RollUpAttendanceCommand command, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<AttendanceRollUpRow>>(async token =>
    {
        // 1. The month's logs, working days, employees and existing records.
        var cmd = command;
        var start = new DateOnly(cmd.Year, cmd.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var month = await logs.ListBetweenAsync(start, end, null, cmd.LegalEntityId, token);
        if (month.Count == 0) return [];
        var calendar = await calendars.LoadAsync(start, end, token);
        var workingDays = calendar.Count(start, end);
        var ids = month.Select(l => l.EmployeeId).Distinct().ToList();
        var staff = await employees.MapByIdsAsync(ids, token);
        var existing = await records.MapForMonthForUpdateAsync(cmd.Year, cmd.Month, ids, token);

        // 2. One monthly record per employee with logs.
        var result = new List<AttendanceRollUpRow>();
        foreach (var g in month.GroupBy(l => l.EmployeeId))
        {
            if (!staff.TryGetValue(g.Key, out var e)) continue;
            var half = g.Count(l => l.Status == DailyAttendanceStatus.HalfDay);
            var present = g.Count(l => l.Status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay);
            var absent = g.Count(l => l.Status == DailyAttendanceStatus.Absent && calendar.IsWorkingDay(l.Date)) + half * 0.5m; // a half day is attended, half of it unpaid
            var leave = g.Count(l => l.Status == DailyAttendanceStatus.Leave);
            var overtime = Math.Round(g.Sum(l => l.OvertimeMinutes) / 60m, 2);
            var lateDays = g.Count(l => l.LateMinutes > 0);
            if (!existing.TryGetValue(g.Key, out var rec))
            {
                rec = new AttendanceRecord { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Year = cmd.Year, Month = cmd.Month };
                records.Add(rec);
            }
            rec.WorkingDays = workingDays; rec.DaysPresent = present; rec.UnpaidAbsentDays = absent; rec.PaidLeaveDays = leave; rec.OvertimeHours = overtime;
            result.Add(new AttendanceRollUpRow(e.Id, e.FullName, workingDays, present, absent, leave, overtime, lateDays, g.Sum(l => l.LateMinutes)));
        }

        // 3. Save.
        await records.SaveChangesAsync(token);
        return result.OrderBy(r => r.EmployeeName).ToList();
    }, ct);
}
