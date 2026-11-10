using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record ListingInput(Guid PropertyId, Guid? UnitId, ListingKind Kind, string Title, decimal Price, string? Channels = null, string? Description = null);

public sealed record ListingDto(Guid Id, Guid LegalEntityId, Guid PropertyId, Guid? UnitId, ListingKind Kind, string Title, decimal Price, ListingStatus Status, string? Channels, string? Description, DateOnly? ListedOn, DateOnly? ClosedOn)
{
    public static ListingDto From(Listing l) => new(l.Id, l.LegalEntityId, l.PropertyId, l.UnitId, l.Kind, l.Title, l.Price, l.Status, l.Channels, l.Description, l.ListedOn, l.ClosedOn);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Listing, ListingDto>> Projection = x => new ListingDto(x.Id, x.LegalEntityId, x.PropertyId, x.UnitId, x.Kind, x.Title, x.Price, x.Status, x.Channels, x.Description, x.ListedOn, x.ClosedOn);
}
