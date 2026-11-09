namespace ERP.Modules.Manufacturing.Features;

public record ManufacturingKpiSummaryDto(
    int ActiveBomsCount,
    int OpenWorkOrdersCount,
    int CompletedWorkOrdersMonth,
    decimal TotalProducedQuantityMonth,
    decimal AverageYieldRatePercentage,
    decimal TotalManufacturingCostMonth);
