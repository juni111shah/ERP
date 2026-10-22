using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

/// <summary>Lookup for units ("Studio", "2 Bedroom", "Office") with the usage and bedrooms a new unit starts with.</summary>
[DefaultSort("code")]
public class UnitType : OrgEntity
{
    [Listable] public string Code { get; set; } = default!;
    [Listable] public string Name { get; set; } = default!;
    [Filterable, Sortable] public UnitUsage DefaultUsage { get; set; } = UnitUsage.Residential;
    public int? DefaultBedrooms { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
}

/// <summary>A building, villa compound or commercial asset owned (or managed) by a legal entity.</summary>
[DefaultSort("code")]
public class Property : LegalEntityScopedEntity, IHasCustomFields
{
    [Listable] public string Code { get; set; } = default!;
    [Listable] public string Name { get; set; } = default!;
    [Filterable, Sortable] public PropertyType PropertyType { get; set; }
    [Filterable] public OwnershipType OwnershipType { get; set; } = OwnershipType.Owned;
    /// <summary>Third-party owner (a core party) when the property is managed for an owner.</summary>
    public Guid? OwnerPartyId { get; set; }
    [Listable] public string? Region { get; set; }
    [Listable] public string? Community { get; set; }
    [Searchable] public string? PlotNumber { get; set; }
    public int? YearBuilt { get; set; }
    /// <summary>Total lettable units; never below the number of registered units.</summary>
    public int TotalUnits { get; set; }
    [Sortable] public decimal IndicativeValue { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
}

/// <summary>
/// Lettable inventory, unique per property by <see cref="UnitNumber"/>. Status follows the unit lifecycle: only lease
/// activation occupies a unit, only lease end/termination vacates it.
/// </summary>
[DefaultSort("unitNumber")]
public class Unit : LegalEntityScopedEntity, IHasCustomFields
{
    [Filterable] public Guid PropertyId { get; set; }
    [Listable] public string UnitNumber { get; set; } = default!;
    [Filterable] public Guid UnitTypeId { get; set; }
    [Filterable, Sortable] public UnitUsage Usage { get; set; }
    [Filterable] public string? Floor { get; set; }
    public int? Bedrooms { get; set; }
    [Sortable] public decimal AreaSqft { get; set; }
    /// <summary>Current annual asking rent (the history is in <see cref="UnitRentRate"/>).</summary>
    [Sortable] public decimal AskingRent { get; set; }
    [Filterable, Sortable] public UnitStatus Status { get; private set; } = UnitStatus.Vacant;
    /// <summary>When the unit last became vacant (drives "days vacant").</summary>
    public DateOnly? VacantSince { get; private set; }
    public DateOnly? ReservedUntil { get; private set; }
    public string? UnavailableReason { get; private set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    public static Unit Create(DateOnly today) => new() { VacantSince = today };

    /// <summary>Vacant -> Reserved (optionally until a date).</summary>
    public void Reserve(DateOnly? until)
    {
        if (Status != UnitStatus.Vacant) throw new DomainException($"Only a vacant unit can be reserved (this unit is {Status}).");
        Status = UnitStatus.Reserved;
        ReservedUntil = until;
    }

    /// <summary>Reserved -> Vacant (cancelled or expired reservation).</summary>
    public void ReleaseReservation(DateOnly today)
    {
        if (Status != UnitStatus.Reserved) throw new DomainException($"Only a reserved unit can be released (this unit is {Status}).");
        Status = UnitStatus.Vacant;
        ReservedUntil = null;
        VacantSince ??= today;
    }

    /// <summary>Vacant/Reserved -> Occupied. Called only by lease activation.</summary>
    public void Occupy()
    {
        if (Status is not (UnitStatus.Vacant or UnitStatus.Reserved)) throw new DomainException($"Unit {UnitNumber} is {Status} and cannot be let.");
        Status = UnitStatus.Occupied;
        ReservedUntil = null;
        VacantSince = null;
    }

    /// <summary>Occupied -> Vacant (lease ended or terminated). A pending repair parks the unit as Unavailable instead.</summary>
    public void Vacate(DateOnly today, bool repairsPending = false)
    {
        if (Status != UnitStatus.Occupied) throw new DomainException($"Unit {UnitNumber} is not occupied.");
        if (repairsPending) { Status = UnitStatus.Unavailable; UnavailableReason = "Repairs pending after move-out"; }
        else { Status = UnitStatus.Vacant; VacantSince = today; }
    }

    /// <summary>Any non-occupied status -> Unavailable (renovation, owner use, legal hold). A reason is required.</summary>
    public void MakeUnavailable(string reason)
    {
        if (Status == UnitStatus.Occupied) throw new DomainException("An occupied unit cannot be made unavailable; end the lease first.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("A reason is required to make a unit unavailable.");
        Status = UnitStatus.Unavailable;
        UnavailableReason = reason.Trim();
        ReservedUntil = null;
    }

    /// <summary>Unavailable -> Vacant (work done).</summary>
    public void MakeAvailable(DateOnly today)
    {
        if (Status != UnitStatus.Unavailable) throw new DomainException($"Only an unavailable unit can be made available (this unit is {Status}).");
        Status = UnitStatus.Vacant;
        UnavailableReason = null;
        VacantSince = today;
    }
}

/// <summary>Effective-dated asking-rent history of a unit.</summary>
public class UnitRentRate : LegalEntityScopedEntity
{
    public Guid UnitId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal AnnualRent { get; set; }
    public string? Reason { get; set; }
}
