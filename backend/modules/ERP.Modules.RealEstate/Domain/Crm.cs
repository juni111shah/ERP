using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

public enum ListingKind { Rent = 1, Sale = 2 }
public enum ListingStatus { Draft = 0, Published = 1, Closed = 2 }
public enum LeadStage { New = 0, Contacted = 1, Viewing = 2, Negotiating = 3, Won = 4, Lost = 9 }

/// <summary>A property or unit offered to the market for rent or sale.</summary>
[DefaultSort("-listedOn")]
public class Listing : LegalEntityScopedEntity
{
    [Filterable] public Guid PropertyId { get; set; }
    [Filterable] public Guid? UnitId { get; set; }
    [Filterable] public ListingKind Kind { get; set; } = ListingKind.Rent;
    [Searchable, Listable] public string Title { get; set; } = default!;
    [Sortable] public decimal Price { get; set; }
    [Filterable] public ListingStatus Status { get; set; } = ListingStatus.Draft;
    /// <summary>Where it is advertised (portals, website, signboard...), free text.</summary>
    [Searchable] public string? Channels { get; set; }
    public string? Description { get; set; }
    [Filterable, Sortable] public DateOnly? ListedOn { get; set; }
    public DateOnly? ClosedOn { get; set; }
}

/// <summary>A prospective client. Moves New, Contacted, Viewing, Negotiating, then Won (converted to a brokerage deal) or Lost.</summary>
[DefaultSort("-createdAt")]
public class Lead : LegalEntityScopedEntity
{
    [Searchable, Listable] public string Name { get; set; } = default!;
    [Searchable] public string? Phone { get; set; }
    [Searchable] public string? Email { get; set; }
    [Filterable, Searchable] public string? Source { get; set; }
    [Searchable] public string? Interest { get; set; }
    [Filterable] public Guid? AgentPartyId { get; set; }
    [Filterable] public LeadStage Stage { get; set; } = LeadStage.New;
    public string? LostReason { get; set; }
    [Filterable] public Guid? ListingId { get; set; }
    [Filterable] public Guid? UnitId { get; set; }
    public string? Notes { get; set; }
    [Filterable, Sortable] public DateOnly? FollowUpOn { get; set; }
    /// <summary>The brokerage deal created when the lead was won.</summary>
    public Guid? DealId { get; set; }
}
