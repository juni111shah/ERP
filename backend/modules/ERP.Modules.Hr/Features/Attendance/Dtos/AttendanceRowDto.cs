namespace ERP.Modules.Hr.Features;

public sealed record AttendanceInput(Guid EmployeeId, int Year, int Month, int WorkingDays, int DaysPresent, decimal UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours);
public sealed record AttendanceRowDto(
    Guid EmployeeId, string EmployeeNumber, string EmployeeName, string Designation, Guid LegalEntityId, Guid? RecordId, bool Recorded, int WorkingDays, int DaysPresent, decimal UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours,
    int LeavePaidDays = 0, int LeaveUnpaidDays = 0);
