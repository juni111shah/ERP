using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

/// <summary>Status blank = Present when there is a check-in, otherwise Absent.</summary>
public sealed record DailyAttendanceInput(Guid EmployeeId, DateOnly Date, TimeOnly? CheckIn = null, TimeOnly? CheckOut = null, DailyAttendanceStatus? Status = null, string? Notes = null, string? Source = null);
public sealed record DailyAttendanceDto(Guid Id, Guid EmployeeId, DateOnly Date, TimeOnly? CheckIn, TimeOnly? CheckOut, DailyAttendanceStatus Status, int WorkedMinutes, int LateMinutes, int OvertimeMinutes, string? Notes, string? Source);
public sealed record BulkDailyAttendanceResult(int Saved, IReadOnlyList<string> Rejected);
public sealed record AttendanceRollUpRow(Guid EmployeeId, string EmployeeName, int WorkingDays, int DaysPresent, decimal UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours, int LateDays, int LateMinutes);

public sealed record ListDailyAttendance(int Year, int Month, Guid? EmployeeId, Guid? LegalEntityId) : IQuery<IReadOnlyList<DailyAttendanceDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
public sealed record SaveDailyAttendance(DailyAttendanceInput Input) : ICommand<DailyAttendanceDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record BulkSaveDailyAttendance(IReadOnlyList<DailyAttendanceInput> Items) : ICommand<BulkDailyAttendanceResult>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record DeleteDailyAttendance(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
/// <summary>Turns the month's daily logs into the monthly attendance record payroll reads (days present, unpaid absence, paid leave, overtime hours). Employees without logs are left alone.</summary>
public sealed record RollUpAttendance(int Year, int Month, Guid? LegalEntityId) : ICommand<IReadOnlyList<AttendanceRollUpRow>>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }

/// <summary>One raw clock event from a device or a phone: the employee number and the moment. The first punch of a day is the check-in, the latest later one the check-out.</summary>
public sealed record PunchInput(string EmployeeNumber, DateTime At);
public sealed record PunchesBody(IReadOnlyList<PunchInput> Punches, string? Source = null);
public sealed record RecordPunches(IReadOnlyList<PunchInput> Punches, string? Source) : ICommand<BulkDailyAttendanceResult>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed class RecordPunchesValidator : AbstractValidator<RecordPunches>
{
    public RecordPunchesValidator()
    {
        RuleFor(x => x.Punches).NotEmpty().Must(p => p.Count <= 5000).WithMessage("Send at most 5000 punches at a time.");
        RuleFor(x => x.Source).MaximumLength(50);
    }
}

/// <summary>Folds one punch into the day's existing times; no I/O.</summary>
public static class PunchMath
{
    public static (TimeOnly CheckIn, TimeOnly? CheckOut) Apply(TimeOnly? checkIn, TimeOnly? checkOut, TimeOnly at)
    {
        if (checkIn is not { } ci) return (at, checkOut is { } o && o > at ? o : null);
        if (at < ci) return (at, checkOut ?? ci);   // an earlier punch arrives late: it becomes the check-in, the old one the check-out
        if (at == ci) return (ci, checkOut);
        return (ci, checkOut is { } co && co >= at ? co : at);
    }
}

public sealed class DailyAttendanceInputValidator : AbstractValidator<DailyAttendanceInput>
{
    public DailyAttendanceInputValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(300);
        RuleFor(x => x.Source).MaximumLength(50);
        RuleFor(x => x.CheckOut!.Value).GreaterThan(x => x.CheckIn!.Value).When(x => x.CheckIn is not null && x.CheckOut is not null).WithName("CheckOut").WithMessage("Check-out must be after check-in.");
        RuleFor(x => x.CheckIn).Must((x, c) => c is null || x.Status is null or DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay).WithMessage("Only a present or half day can have a check-in.");
    }
}
public sealed class SaveDailyAttendanceValidator : AbstractValidator<SaveDailyAttendance> { public SaveDailyAttendanceValidator() => RuleFor(x => x.Input).SetValidator(new DailyAttendanceInputValidator()); }
public sealed class ListDailyAttendanceValidator : AbstractValidator<ListDailyAttendance>
{
    public ListDailyAttendanceValidator() { RuleFor(x => x.Year).InclusiveBetween(2000, 2100); RuleFor(x => x.Month).InclusiveBetween(1, 12); }
}
public sealed class RollUpAttendanceValidator : AbstractValidator<RollUpAttendance>
{
    public RollUpAttendanceValidator() { RuleFor(x => x.Year).InclusiveBetween(2000, 2100); RuleFor(x => x.Month).InclusiveBetween(1, 12); }
}
public sealed class BulkSaveDailyAttendanceValidator : AbstractValidator<BulkSaveDailyAttendance>
{
    public BulkSaveDailyAttendanceValidator() { RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 2000).WithMessage("Import at most 2000 rows at a time."); }
}

/// <summary>Minutes worked, late and overtime for one day; no I/O.</summary>
public static class DailyAttendanceMath
{
    public sealed record Result(int Worked, int Late, int Overtime);

    public static Result Compute(TimeOnly? checkIn, TimeOnly? checkOut, TimeOnly shiftStart, int graceMinutes, int hoursPerDay)
    {
        if (checkIn is not { } inT) return new Result(0, 0, 0);
        var late = Math.Max(0, (int)(inT - shiftStart).TotalMinutes - graceMinutes);
        if (late > 0) late += graceMinutes; // beyond the grace period the whole delay counts, not only the part after it
        if (checkOut is not { } outT || outT <= inT) return new Result(0, late, 0);
        var worked = (int)(outT - inT).TotalMinutes;
        return new Result(worked, late, Math.Max(0, worked - hoursPerDay * 60));
    }

    public static DailyAttendanceStatus StatusOf(DailyAttendanceStatus? given, TimeOnly? checkIn) => given ?? (checkIn is null ? DailyAttendanceStatus.Absent : DailyAttendanceStatus.Present);
}

internal sealed class DailyAttendanceHandlers(
    IRepository<AttendanceLog> logs, IRepository<AttendanceRecord> records, IReadRepository<Employee> employees, IReadRepository<Domain.Shift> shiftRepo, LeaveCalendarProvider calendars, ISettingsService settings,
    ILegalEntityGuard guard, IUnitOfWork uow) :
    IRequestHandler<ListDailyAttendance, IReadOnlyList<DailyAttendanceDto>>,
    IRequestHandler<SaveDailyAttendance, DailyAttendanceDto>,
    IRequestHandler<BulkSaveDailyAttendance, BulkDailyAttendanceResult>,
    IRequestHandler<DeleteDailyAttendance, Unit>,
    IRequestHandler<RollUpAttendance, IReadOnlyList<AttendanceRollUpRow>>,
    IRequestHandler<RecordPunches, BulkDailyAttendanceResult>
{
    public async Task<IReadOnlyList<DailyAttendanceDto>> Handle(ListDailyAttendance q, CancellationToken ct)
    {
        var start = new DateOnly(q.Year, q.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var rows = await logs.Query(new Specification<AttendanceLog>(l => l.Date >= start && l.Date <= end && (q.EmployeeId == null || l.EmployeeId == q.EmployeeId) && (q.LegalEntityId == null || l.LegalEntityId == q.LegalEntityId))).ToListAsync(ct);
        return rows.OrderBy(l => l.Date).ThenBy(l => l.EmployeeId).Select(ToDto).ToList();
    }

    public async Task<DailyAttendanceDto> Handle(SaveDailyAttendance cmd, CancellationToken ct)
    {
        var shift = await ShiftAsync(null, ct);
        var log = await UpsertAsync(cmd.Input, shift, ct);
        await logs.SaveChangesAsync(ct);
        return ToDto(log);
    }

    public Task<BulkDailyAttendanceResult> Handle(BulkSaveDailyAttendance cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var shift = await ShiftAsync(null, token);
        var saved = 0; var rejected = new List<string>();
        foreach (var item in cmd.Items)
        {
            try { await UpsertAsync(item with { Source = item.Source ?? "import" }, shift, token); await logs.SaveChangesAsync(token); saved++; }
            catch (Exception e) when (e is NotFoundException or ConflictException or FieldValidationException) { rejected.Add($"{item.Date:yyyy-MM-dd}: {e.Message}"); }
        }
        return new BulkDailyAttendanceResult(saved, rejected);
    }, ct);

    public Task<BulkDailyAttendanceResult> Handle(RecordPunches cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var shift = await ShiftAsync(null, token);
        var saved = 0; var rejected = new List<string>();
        foreach (var p in cmd.Punches.OrderBy(p => p.At))
        {
            var number = p.EmployeeNumber.Trim();
            try
            {
                var emp = await employees.Query(new Specification<Employee>(e => e.Number == number)).FirstOrDefaultAsync(token) ?? throw new NotFoundException($"unknown employee \"{number}\"");
                var date = DateOnly.FromDateTime(p.At); var empId = emp.Id;
                var day = await logs.Query(new Specification<AttendanceLog>(l => l.EmployeeId == empId && l.Date == date)).FirstOrDefaultAsync(token);
                var (ci, co) = PunchMath.Apply(day?.Status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay ? day.CheckIn : null, day?.Status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay ? day.CheckOut : null, TimeOnly.FromDateTime(p.At));
                var status = day?.Status == DailyAttendanceStatus.HalfDay ? DailyAttendanceStatus.HalfDay : DailyAttendanceStatus.Present;
                await UpsertAsync(new DailyAttendanceInput(emp.Id, date, ci, co, status, day?.Notes, cmd.Source ?? "device"), shift, token);
                await logs.SaveChangesAsync(token); saved++;
            }
            catch (Exception e) when (e is NotFoundException or ConflictException or FieldValidationException) { rejected.Add($"{number} {p.At:yyyy-MM-dd HH:mm}: {e.Message}"); }
        }
        return new BulkDailyAttendanceResult(saved, rejected);
    }, ct);

    public async Task<Unit> Handle(DeleteDailyAttendance cmd, CancellationToken ct)
    {
        logs.Remove(await logs.GetForUpdateAsync(cmd.Id, ct));
        await logs.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<IReadOnlyList<AttendanceRollUpRow>> Handle(RollUpAttendance cmd, CancellationToken ct) => uow.InTransactionAsync<IReadOnlyList<AttendanceRollUpRow>>(async token =>
    {
        var start = new DateOnly(cmd.Year, cmd.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var month = await logs.Query(new Specification<AttendanceLog>(l => l.Date >= start && l.Date <= end && (cmd.LegalEntityId == null || l.LegalEntityId == cmd.LegalEntityId))).ToListAsync(token);
        if (month.Count == 0) return [];
        var calendar = await calendars.LoadAsync(start, end, token);
        var workingDays = calendar.Count(start, end);
        var ids = month.Select(l => l.EmployeeId).Distinct().ToList();
        var staff = (await employees.Query(new Specification<Employee>(e => ids.Contains(e.Id))).ToListAsync(token)).ToDictionary(e => e.Id);
        var existing = (await records.Tracked(new Specification<AttendanceRecord>(r => r.Year == cmd.Year && r.Month == cmd.Month && ids.Contains(r.EmployeeId))).ToListAsync(token)).ToDictionary(r => r.EmployeeId);
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
        await records.SaveChangesAsync(token);
        return result.OrderBy(r => r.EmployeeName).ToList();
    }, ct);

    private sealed record ShiftRule(TimeOnly Start, int GraceMinutes, int HoursPerDay);

    private async Task<ShiftRule> ShiftAsync(Guid? entityId, CancellationToken ct)
    {
        var start = await settings.GetAsync<string?>("hr.shiftStart", entityId, ct);
        var grace = await settings.GetAsync<int?>("hr.lateGraceMinutes", entityId, ct) ?? 10;
        var hours = await settings.GetAsync<int?>("hr.hoursPerDay", entityId, ct) ?? 8;
        return new ShiftRule(TimeOnly.TryParse(start, out var s) ? s : new TimeOnly(8, 0), grace, hours);
    }

    private async Task<AttendanceLog> UpsertAsync(DailyAttendanceInput i, ShiftRule defaults, CancellationToken ct)
    {
        var emp = await employees.FindAsync(i.EmployeeId, ct) ?? throw new NotFoundException($"Employee {i.EmployeeId} was not found.");
        var shift = defaults;
        if (emp.ShiftId is { } shiftId && await shiftRepo.FindAsync(shiftId, ct) is { } own) shift = new ShiftRule(own.StartTime, own.GraceMinutes, own.HoursPerDay); // the employee's own shift wins over the workspace settings
        await guard.RequireAsync(emp.LegalEntityId, ct);
        if (i.Date < emp.JoinDate || (emp.TerminationDate is { } t && i.Date > t)) throw new ConflictException("The employee was not employed on this date.");
        var status = DailyAttendanceMath.StatusOf(i.Status, i.CheckIn);
        var worked = status is DailyAttendanceStatus.Present or DailyAttendanceStatus.HalfDay;
        var m = worked ?DailyAttendanceMath.Compute(i.CheckIn, i.CheckOut, shift.Start, shift.GraceMinutes, shift.HoursPerDay) : new DailyAttendanceMath.Result(0, 0, 0);
        var date = i.Date; var empId = emp.Id;
        var log = await logs.Tracked(new Specification<AttendanceLog>(l => l.EmployeeId == empId && l.Date == date)).FirstOrDefaultAsync(ct);
        if (log is null) { log = new AttendanceLog { LegalEntityId = emp.LegalEntityId, EmployeeId = emp.Id, Date = i.Date }; logs.Add(log); }
        log.CheckIn = worked ? i.CheckIn : null; log.CheckOut = worked ? i.CheckOut : null; log.Status = status;
        log.WorkedMinutes = m.Worked; log.LateMinutes = m.Late; log.OvertimeMinutes = m.Overtime; log.Notes = i.Notes?.Trim(); log.Source = i.Source ?? "manual";
        return log;
    }

    private static DailyAttendanceDto ToDto(AttendanceLog l) => new(l.Id, l.EmployeeId, l.Date, l.CheckIn, l.CheckOut, l.Status, l.WorkedMinutes, l.LateMinutes, l.OvertimeMinutes, l.Notes, l.Source);
}
