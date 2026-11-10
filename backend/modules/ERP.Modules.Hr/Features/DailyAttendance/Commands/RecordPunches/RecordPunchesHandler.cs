using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class RecordPunchesHandler(IAttendanceLogRepository logs, IEmployeeRepository employees, DailyAttendanceRecorder recorder, IUnitOfWork uow) : ICommandHandler<RecordPunchesCommand, BulkDailyAttendanceResult>
{
    public Task<BulkDailyAttendanceResult> Handle(RecordPunchesCommand command, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var cmd = command;
        var shift = await recorder.ShiftAsync(null, token);
        var saved = 0; var rejected = new List<string>();
        foreach (var p in cmd.Punches.OrderBy(p => p.At))
        {
            var number = p.EmployeeNumber.Trim();
            try
            {
                // 1. The employee by number and what the day already holds.
                var emp = await employees.FindByNumberAsync(number, token) ?? throw new NotFoundException($"unknown employee \"{number}\"");
                var date = DateOnly.FromDateTime(p.At);
                var day = await logs.FindForDayAsync(emp.Id, date, token);

                // 2. Fold the punch into the day and save it.
                var (ci, co) = PunchMath.Apply(day?.Status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay ? day.CheckIn : null, day?.Status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay ? day.CheckOut : null, TimeOnly.FromDateTime(p.At));
                var status = day?.Status == DailyAttendanceStatus.HalfDay ? DailyAttendanceStatus.HalfDay : DailyAttendanceStatus.Present;
                await recorder.UpsertAsync(new DailyAttendanceInput(emp.Id, date, ci, co, status, day?.Notes, cmd.Source ?? "device"), shift, token);
                await logs.SaveChangesAsync(token); saved++;
            }
            catch (Exception e) when (e is NotFoundException or ConflictException or FieldValidationException) { rejected.Add($"{number} {p.At:yyyy-MM-dd HH:mm}: {e.Message}"); }
        }
        return new BulkDailyAttendanceResult(saved, rejected);
    }, ct);
}
