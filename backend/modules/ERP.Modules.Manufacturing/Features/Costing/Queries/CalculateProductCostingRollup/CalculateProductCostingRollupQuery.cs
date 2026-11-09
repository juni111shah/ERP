using ERP.Application.Common;

namespace ERP.Modules.Manufacturing.Features;

/// <summary>The standard unit cost of a BOM with its component breakdown; null when the BOM does not exist. WarehouseId says whose average to read for lines with no typed cost; without it the stock service uses the workspace's first active warehouse.</summary>
public record CalculateProductCostingRollupQuery(Guid BomId, Guid? WarehouseId = null) : IQuery<ProductCostRollupDto?>;
