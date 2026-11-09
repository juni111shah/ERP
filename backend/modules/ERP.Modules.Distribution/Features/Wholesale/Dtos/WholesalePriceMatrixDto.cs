using System.Linq.Expressions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record WholesalePriceMatrixDto(
    Guid Id,
    Guid LegalEntityId,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    WholesaleTier Tier,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercentage,
    decimal WholesaleUnitPrice,
    bool IsActive,
    string? Notes,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<WholesalePriceMatrix, WholesalePriceMatrixDto>> Projection = w => new WholesalePriceMatrixDto(
        w.Id, w.LegalEntityId, w.ItemId, w.ItemCode, w.ItemName,
        w.Tier, w.MinQuantity, w.MaxQuantity, w.DiscountPercentage,
        w.WholesaleUnitPrice, w.IsActive, w.Notes, w.CreatedAt);

    public static WholesalePriceMatrixDto From(WholesalePriceMatrix p) => new(
        p.Id,
        p.LegalEntityId,
        p.ItemId,
        p.ItemCode,
        p.ItemName,
        p.Tier,
        p.MinQuantity,
        p.MaxQuantity,
        p.DiscountPercentage,
        p.WholesaleUnitPrice,
        p.IsActive,
        p.Notes,
        p.CreatedAt);
}
