namespace ERP.Modules.Manufacturing.Features;

public record BomComponentInput(
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    string Uom,
    decimal ScrapPercentage,
    decimal? UnitCost,
    string? Notes);
