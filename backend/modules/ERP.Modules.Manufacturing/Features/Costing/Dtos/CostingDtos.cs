namespace ERP.Modules.Manufacturing.Features;

public record ProductCostRollupDto(
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    decimal MaterialCost,
    decimal LaborCost,
    decimal OverheadCost,
    decimal TotalStandardUnitCost,
    List<CostComponentBreakdownDto> ComponentBreakdown);

public record CostComponentBreakdownDto(
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    decimal UnitCost,
    decimal TotalCost,
    decimal CostSharePercentage);

public record ProductCostVarianceDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal StandardUnitCost,
    decimal ActualAverageUnitCost,
    decimal VarianceAmount,
    decimal VariancePercentage,
    int CompletedWorkOrdersCount);
