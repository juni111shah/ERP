using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

/// <summary>Changes a wholesale price tier.</summary>
public record UpdateWholesalePriceCommand(
    Guid Id,
    WholesaleTier Tier,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercentage,
    decimal WholesaleUnitPrice,
    bool IsActive,
    string? Notes) : ICommand<WholesalePriceMatrixDto>;
