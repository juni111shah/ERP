using System.Linq.Expressions;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

public record RetailPriceListSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    PriceListType Type,
    string Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    int ItemCount,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<RetailPriceList, RetailPriceListSummaryDto>> Projection = p => new RetailPriceListSummaryDto(
        p.Id, p.LegalEntityId, p.Code, p.Name, p.Type, p.Currency,
        p.ValidFrom, p.ValidTo, p.IsDefault, p.IsActive, p.Items.Count, p.CreatedAt);
}
