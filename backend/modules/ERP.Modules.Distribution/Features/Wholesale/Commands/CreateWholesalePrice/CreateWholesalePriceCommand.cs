using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

/// <summary>Adds a wholesale price tier for an item.</summary>
public record CreateWholesalePriceCommand(
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
    string? Notes) : ICommand<WholesalePriceMatrixDto>;
