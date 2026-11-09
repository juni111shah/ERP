using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Status blank = Present when there is a check-in, otherwise Absent.</summary>
public sealed record DailyAttendanceInput(Guid EmployeeId, DateOnly Date, TimeOnly? CheckIn = null, TimeOnly? CheckOut = null, DailyAttendanceStatus? Status = null, string? Notes = null, string? Source = null);
public sealed record DailyAttendanceDto(Guid Id, Guid EmployeeId, DateOnly Date, TimeOnly? CheckIn, TimeOnly? CheckOut, DailyAttendanceStatus Status, int WorkedMinutes, int LateMinutes, int OvertimeMinutes, string? Notes, string? Source)
{
    public static DailyAttendanceDto From(AttendanceLog l) => new(l.Id, l.EmployeeId, l.Date, l.CheckIn, l.CheckOut, l.Status, l.WorkedMinutes, l.LateMinutes, l.OvertimeMinutes, l.Notes, l.Source);
}
public sealed record BulkDailyAttendanceResult(int Saved, IReadOnlyList<string> Rejected);
public sealed record AttendanceRollUpRow(Guid EmployeeId, string EmployeeName, int WorkingDays, int DaysPresent, decimal UnpaidAbsentDays, int PaidLeaveDays, decimal OvertimeHours, int LateDays, int LateMinutes);

/// <summary>One raw clock event from a device or a phone: the employee number and the moment. The first punch of a day is the check-in, the latest later one the check-out.</summary>
public sealed record PunchInput(string EmployeeNumber, DateTime At);
