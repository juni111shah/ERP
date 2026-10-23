using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

/// <summary>
/// A recurring maintenance task (AC service, fire-pump test, lift inspection). The nightly job raises a work order
/// <see cref="LeadDays"/> before <see cref="NextDue"/> and moves NextDue forward by <see cref="FrequencyMonths"/>.
/// </summary>
[DefaultSort("nextDue")]
public class PmSchedule : LegalEntityScopedEntity
{
    [Filterable] public Guid PropertyId { get; set; }
    /// <summary>Null = common areas / whole property.</summary>
    [Filterable] public Guid? UnitId { get; set; }
    [Searchable, Listable] public string Title { get; set; } = default!;
    [Filterable, Searchable] public string? Category { get; set; }
    [Filterable] public Priority Priority { get; set; } = Priority.Normal;
    public int FrequencyMonths { get; set; } = 3;
    [Filterable, Sortable] public DateOnly NextDue { get; set; }
    public int LeadDays { get; set; } = 7;
    public decimal EstimatedCost { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
    public DateOnly? LastGeneratedOn { get; set; }
    public string? Notes { get; set; }
}
