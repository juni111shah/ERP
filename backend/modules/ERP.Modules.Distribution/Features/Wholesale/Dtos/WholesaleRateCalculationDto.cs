using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

public record WholesaleRateCalculationDto(
    Guid ItemId,
    WholesaleTier Tier,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercentage,
    decimal ExtendedTotal,
    bool TierApplied);
