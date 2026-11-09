using System.Linq.Expressions;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

public record RetailPromotionDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PromotionType Type,
    decimal DiscountValue,
    decimal? MinimumOrderAmount,
    DateOnly StartDate,
    DateOnly EndDate,
    int? UsageLimit,
    int TimesUsed,
    bool IsActive,
    string? Description,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<RetailPromotion, RetailPromotionDto>> Projection = p => new RetailPromotionDto(
        p.Id, p.LegalEntityId, p.Code, p.Name, p.Type, p.DiscountValue,
        p.MinimumOrderAmount, p.StartDate, p.EndDate, p.UsageLimit,
        p.TimesUsed, p.IsActive, p.Description, p.CustomFields, p.CreatedAt);

    public static RetailPromotionDto From(RetailPromotion p) => new(
        p.Id,
        p.LegalEntityId,
        p.Code,
        p.Name,
        p.Type,
        p.DiscountValue,
        p.MinimumOrderAmount,
        p.StartDate,
        p.EndDate,
        p.UsageLimit,
        p.TimesUsed,
        p.IsActive,
        p.Description,
        p.CustomFields,
        p.CreatedAt);
}
