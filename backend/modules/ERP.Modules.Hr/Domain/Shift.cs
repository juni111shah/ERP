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
