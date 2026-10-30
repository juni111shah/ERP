using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Hr.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record AttendanceInput(Guid EmployeeId, int Year, int Month, int WorkingDays, int DaysPresent, int UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours);
public sealed record AttendanceRowDto(
    Guid EmployeeId, string EmployeeNumber, string EmployeeName, string Designation, Guid LegalEntityId, Guid? RecordId, bool Recorded, int WorkingDays, int DaysPresent, int UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours,
    int LeavePaidDays = 0, int LeaveUnpaidDays = 0);

/// <summary>Every active employee for a month with their recorded exceptions (or full attendance when nothing is recorded), plus the days of approved leave (read-only; payroll adds the unpaid ones to the recorded absences).</summary>
public sealed record ListAttendance(int Year, int Month, Guid? LegalEntityId) : IQuery<IReadOnlyList<AttendanceRowDto>>, IRequirePermission { public string RequiredPermission => P.AttendanceRead; }
public sealed record SaveAttendance(AttendanceInput Input) : ICommand<AttendanceRowDto>, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }
public sealed record DeleteAttendance(Guid RecordId) : ICommand, IRequirePermission { public string RequiredPermission => P.AttendanceManage; }

public sealed class SaveAttendanceValidator : AbstractValidator<SaveAttendance>
{
    public SaveAttendanceValidator()
    {
        RuleFor(x => x.Input.EmployeeId).NotEmpty();
        RuleFor(x => x.Input.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Input.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Input.WorkingDays).InclusiveBetween(1, 31);
        RuleFor(x => x.Input.DaysPresent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.UnpaidAbsentDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.PaidLeaveDays).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input.OvertimeHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Input).Must(i => i.DaysPresent <= i.WorkingDays).WithName("daysPresent").WithMessage("Days present cannot exceed the working days.");
        RuleFor(x => x.Input).Must(i => i.UnpaidAbsentDays + i.PaidLeaveDays <= i.WorkingDays).WithName("unpaidAbsentDays").WithMessage("Absence and leave cannot exceed the working days.");
    }
}

internal sealed class AttendanceHandlers(IRepository<AttendanceRecord> records, IReadRepository<Employee> employees, LeaveFeed leaveFeed, IReadRepository<PayrollRun> payrollRuns) :
    IRequestHandler<ListAttendance, IReadOnlyList<AttendanceRowDto>>,
    IRequestHandler<SaveAttendance, AttendanceRowDto>,
    IRequestHandler<DeleteAttendance, Unit>
{
    public async Task<IReadOnlyList<AttendanceRowDto>> Handle(ListAttendance q, CancellationToken ct)
    {
        var entity = q.LegalEntityId;
        var start = new DateOnly(q.Year, q.Month, 1); var end = start.AddMonths(1).AddDays(-1);
        var staff = (await employees.Query(new Specification<Employee>(e => entity == null || e.LegalEntityId == entity)).ToListAsync(ct)).Where(e => e.IsPayableIn(start, end)).OrderBy(e => e.Number).ToList();
        var ids = staff.Select(s => s.Id).ToList();
        var recorded = (await records.Query(new Specification<AttendanceRecord>(r => r.Year == q.Year && r.Month == q.Month && ids.Contains(r.EmployeeId))).ToListAsync(ct)).ToDictionary(r => r.EmployeeId);
        var leave = await leaveFeed.MonthAsync(q.Year, q.Month, ids, ct);
        return staff.Select(s =>
        {
            var (paid, unpaid) = leave.GetValueOrDefault(s.Id);
            return recorded.TryGetValue(s.Id, out var r)
                ? new AttendanceRowDto(s.Id, s.Number, s.FullName, s.Designation, s.LegalEntityId, r.Id, true, r.WorkingDays, r.DaysPresent, r.UnpaidAbsentDays, r.PaidLeaveDays, r.OvertimeHours, paid, unpaid)
                : new AttendanceRowDto(s.Id, s.Number, s.FullName, s.Designation, s.LegalEntityId, null, false, 26, 26, 0, 0, 0, paid, unpaid);
        }).ToList();
    }

    public async Task<AttendanceRowDto> Handle(SaveAttendance cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var e = await employees.GetAsync(i.EmployeeId, ct);
        var monthStart = new DateOnly(i.Year, i.Month, 1);
        if (!e.IsPayableIn(monthStart, monthStart.AddMonths(1).AddDays(-1))) throw new FieldValidationException("employeeId", "This employee is not on the payroll in that month.");
        await EnsureNotPostedAsync(e.LegalEntityId, i.Year, i.Month, ct);
        var r = await records.Tracked(new Specification<AttendanceRecord>(x => x.EmployeeId == e.Id && x.Year == i.Year && x.Month == i.Month)).FirstOrDefaultAsync(ct);
        if (r is null) { r = new AttendanceRecord { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Year = i.Year, Month = i.Month }; records.Add(r); }
        r.WorkingDays = i.WorkingDays; r.DaysPresent = i.DaysPresent; r.UnpaidAbsentDays = i.UnpaidAbsentDays; r.PaidLeaveDays = i.PaidLeaveDays; r.OvertimeHours = i.OvertimeHours;
        await records.SaveChangesAsync(ct);
        return new AttendanceRowDto(e.Id, e.Number, e.FullName, e.Designation, e.LegalEntityId, r.Id, true, r.WorkingDays, r.DaysPresent, r.UnpaidAbsentDays, r.PaidLeaveDays, r.OvertimeHours);
    }

    private async Task EnsureNotPostedAsync(Guid entityId, int year, int month, CancellationToken ct)
    {
        if (await payrollRuns.AnyAsync(p => p.LegalEntityId == entityId && p.Year == year && p.Month == month && (p.Status == PayrollStatus.Posted || p.Status == PayrollStatus.Paid), ct))
            throw new ConflictException("The payroll for this month is already posted, so its attendance can no longer change.");
    }

    public async Task<Unit> Handle(DeleteAttendance cmd, CancellationToken ct)
    {
        var record = await records.GetForUpdateAsync(cmd.RecordId, ct);
        await EnsureNotPostedAsync(record.LegalEntityId, record.Year, record.Month, ct);
        records.Remove(record);
        await records.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
