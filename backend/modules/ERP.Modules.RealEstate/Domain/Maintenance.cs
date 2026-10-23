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

public enum InspectionKind { MoveIn = 1, MoveOut = 2, Routine = 3 }
public enum InspectionStatus { Draft = 0, Completed = 1 }
public enum ItemCondition { Good = 1, Fair = 2, Poor = 3, Damaged = 4 }

/// <summary>A condition check of a unit or property (move-in, move-out, routine) with a checklist of areas. Completing it can raise repair work orders.</summary>
[DefaultSort("-inspectionDate")]
public class Inspection : LegalEntityScopedEntity
{
    [Filterable] public Guid PropertyId { get; set; }
    [Filterable] public Guid? UnitId { get; set; }
    [Filterable] public Guid? LeaseId { get; set; }
    [Filterable] public InspectionKind Kind { get; set; } = InspectionKind.Routine;
    [Filterable, Sortable] public DateOnly InspectionDate { get; set; }
    [Searchable] public string? Inspector { get; set; }
    [Filterable] public InspectionStatus Status { get; set; } = InspectionStatus.Draft;
    public string? Notes { get; set; }
    /// <summary>Sum of the repair costs of poor/damaged items, kept in step with the items (use it when settling a deposit).</summary>
    public decimal DamageCost { get; set; }
    public List<InspectionItem> Items { get; set; } = new();
}

public class InspectionItem : Entity
{
    public Guid InspectionId { get; set; }
    public string Area { get; set; } = default!;
    public ItemCondition Condition { get; set; } = ItemCondition.Good;
    public string? Notes { get; set; }
    public decimal RepairCost { get; set; }
    public Guid? WorkOrderId { get; set; }
}
