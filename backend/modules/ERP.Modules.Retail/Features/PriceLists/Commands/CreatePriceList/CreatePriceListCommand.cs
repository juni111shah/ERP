using ERP.Application.Common;
using ERP.Modules.Retail.Domain;

namespace ERP.Modules.Retail.Features;

/// <summary>Creates a price list with its rows.</summary>
public record CreatePriceListCommand(
    Guid LegalEntityId,
    string Code,
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
