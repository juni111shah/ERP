using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

/// <summary>Updates a price list; the rows sent replace the current ones.</summary>
public record UpdatePriceListCommand(
    Guid Id,
    string Name,
    PriceListType Type,
    string? Currency,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    bool IsDefault,
    bool IsActive,
    string? Description,
    string? CustomFields,
    List<RetailPriceListItemInput> Items) : ICommand<RetailPriceListDto>;
