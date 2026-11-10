using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Hr.Domain;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

internal sealed class SaveAttendanceHandler(IAttendanceRecordRepository records, IReadRepository<Employee> employees, AttendancePeriodGuard period) : ICommandHandler<SaveAttendanceCommand, AttendanceRowDto>
{
    public async Task<AttendanceRowDto> Handle(SaveAttendanceCommand command, CancellationToken ct)
    {
        // 1. The employee is on the payroll in the month, and the month is not posted.
        var i = command.Input;
        var e = await employees.GetAsync(i.EmployeeId, ct);
        var monthStart = new DateOnly(i.Year, i.Month, 1);
        if (!e.IsPayableIn(monthStart, monthStart.AddMonths(1).AddDays(-1))) throw new FieldValidationException("employeeId", "This employee is not on the payroll in that month.");
        await period.EnsureNotPostedAsync(e.LegalEntityId, i.Year, i.Month, ct);

        // 2. Create or update the month's record, and save.
        var r = await records.FindForMonthForUpdateAsync(e.Id, i.Year, i.Month, ct);
        if (r is null) { r = new AttendanceRecord { LegalEntityId = e.LegalEntityId, EmployeeId = e.Id, Year = i.Year, Month = i.Month }; records.Add(r); }
        r.WorkingDays = i.WorkingDays; r.DaysPresent = i.DaysPresent; r.UnpaidAbsentDays = i.UnpaidAbsentDays; r.PaidLeaveDays = i.PaidLeaveDays; r.OvertimeHours = i.OvertimeHours;
        await records.SaveChangesAsync(ct);
        return new AttendanceRowDto(e.Id, e.Number, e.FullName, e.Designation, e.LegalEntityId, r.Id, true, r.WorkingDays, r.DaysPresent, r.UnpaidAbsentDays, r.PaidLeaveDays, r.OvertimeHours);
    }
}
