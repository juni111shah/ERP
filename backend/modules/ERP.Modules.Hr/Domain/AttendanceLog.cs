using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

public enum DailyAttendanceStatus { Present = 1, Absent = 2, Leave = 3, Holiday = 4, WeeklyOff = 5, HalfDay = 6 }

/// <summary>One employee's attendance on one day: check-in and check-out (typed in, imported from a device or a file) with the late and overtime minutes worked out on save. Rolled up into the monthly record that payroll reads.</summary>
[DefaultSort("-date")]
public class AttendanceLog : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    [Sortable, Filterable] public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    [Filterable] public DailyAttendanceStatus Status { get; set; } = DailyAttendanceStatus.Present;
    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int OvertimeMinutes { get; set; }
    public string? Notes { get; set; }
    /// <summary>"manual" or the name of the import (device, file).</summary>
    public string? Source { get; set; }
}
