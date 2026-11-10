using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Features;

/// <summary>The wholesale price of a quantity of an item for a tier, from the matching active rule.</summary>
public record CalculateWholesaleRateQuery(
    Guid LegalEntityId,
    Guid ItemId,
    WholesaleTier Tier,
    decimal Quantity) : IQuery<WholesaleRateCalculationDto>;
