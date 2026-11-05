using ERP.Domain.Common;

namespace ERP.Modules.Hr.Domain;

/// <summary>A work shift: start time, grace for late arrival and the hours that make a normal day. Employees with a shift are measured against it; the rest use the workspace settings (<c>hr.shiftStart</c> ...).</summary>
[DefaultSort("code")]
public class Shift : OrgEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Searchable, Sortable] public string Name { get; set; } = default!;
    public TimeOnly StartTime { get; set; } = new(8, 0);
    public int GraceMinutes { get; set; } = 10;
    public int HoursPerDay { get; set; } = 8;
    [Filterable] public bool IsActive { get; set; } = true;
}

/// <summary>A shift worked on specific dates (rotation, cover, a season): overrides the employee's default shift from FromDate to ToDate.</summary>
public class ShiftRoster : LegalEntityScopedEntity
{
    [Filterable] public Guid EmployeeId { get; set; }
    public Guid ShiftId { get; set; }
    [Sortable, Filterable] public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
}
