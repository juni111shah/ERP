using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>An item's standard unit cost against the actual average of its completed work orders; null when it has neither.</summary>
public record GetProductCostVarianceQuery(Guid LegalEntityId, Guid ItemId) : IQuery<ProductCostVarianceDto?>;
