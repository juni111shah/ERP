using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>Creates or updates one day's attendance log with its worked, late and overtime minutes; used by the manual, import and punch handlers.</summary>
public sealed class DailyAttendanceRecorder(
    IAttendanceLogRepository logs, IReadRepository<Employee> employees, IReadRepository<Domain.Shift> shiftRepo, IShiftRosterRepository rosterRepo, ISettingsService settings, ILegalEntityGuard guard)
{
    public sealed record ShiftRule(TimeOnly Start, int GraceMinutes, int HoursPerDay);

    public async Task<ShiftRule> ShiftAsync(Guid? entityId, CancellationToken ct)
    {
        var start = await settings.GetAsync<string?>("hr.shiftStart", entityId, ct);
        var grace = await settings.GetAsync<int?>("hr.lateGraceMinutes", entityId, ct) ?? 10;
        var hours = await settings.GetAsync<int?>("hr.hoursPerDay", entityId, ct) ?? 8;
        return new ShiftRule(TimeOnly.TryParse(start, out var s) ? s : new TimeOnly(8, 0), grace, hours);
    }

    public async Task<AttendanceLog> UpsertAsync(DailyAttendanceInput i, ShiftRule defaults, CancellationToken ct)
    {
        var emp = await employees.FindAsync(i.EmployeeId, ct) ?? throw new NotFoundException($"Employee {i.EmployeeId} was not found.");
        var shift = defaults;
        var covering = RosterMath.ShiftOn(await rosterRepo.ListCoveringAsync(emp.Id, i.Date, ct), i.Date);
        if ((covering ?? emp.ShiftId) is { } shiftId && await shiftRepo.FindAsync(shiftId, ct) is { } own) shift = new ShiftRule(own.StartTime, own.GraceMinutes, own.HoursPerDay); // a dated roster entry beats the default shift, which beats the workspace settings
        await guard.RequireAsync(emp.LegalEntityId, ct);
        if (i.Date < emp.JoinDate || (emp.TerminationDate is { } t && i.Date > t)) throw new ConflictException("The employee was not employed on this date.");
        var status = DailyAttendanceMath.StatusOf(i.Status, i.CheckIn);
        var worked = status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay;
        var m = worked ?DailyAttendanceMath.Compute(i.CheckIn, i.CheckOut, shift.Start, shift.GraceMinutes, shift.HoursPerDay) : new DailyAttendanceMath.Result(0, 0, 0);
        var log = await logs.FindForDayForUpdateAsync(emp.Id, i.Date, ct);
        if (log is null) { log = new AttendanceLog { LegalEntityId = emp.LegalEntityId, EmployeeId = emp.Id, Date = i.Date }; logs.Add(log); }
        log.CheckIn = worked ? i.CheckIn : null; log.CheckOut = worked ? i.CheckOut : null; log.Status = status;
        log.WorkedMinutes = m.Worked; log.LateMinutes = m.Late; log.OvertimeMinutes = m.Overtime; log.Notes = i.Notes?.Trim(); log.Source = i.Source ?? "manual";
        return log;
    }
}
