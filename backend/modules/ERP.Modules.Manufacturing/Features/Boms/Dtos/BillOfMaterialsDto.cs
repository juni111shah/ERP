using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

public record BomComponentDto(
    Guid Id,
    Guid BomId,
    Guid ComponentItemId,
    string ComponentItemCode,
    string ComponentItemName,
    decimal QuantityRequired,
    string Uom,
    decimal ScrapPercentage,
    decimal UnitCost,
    decimal ExtendedCost,
    string? Notes,
    bool UseCurrentCost = false);

public record BillOfMaterialsDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal LaborCostPerUnit,
    decimal OverheadCostPerUnit,
    decimal TotalMaterialCost,
    decimal TotalUnitCost,
    bool IsActive,
    string? Description,
    int ComponentCount,
    List<BomComponentDto> Components,
    string? CustomFields,
    DateTimeOffset CreatedAt)
{
    public static BillOfMaterialsDto From(BillOfMaterials b)
    {
        var totalMaterial = b.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)));
        var totalUnitCost = b.OutputQuantity > 0
            ? Math.Round((totalMaterial / b.OutputQuantity) + b.LaborCostPerUnit + b.OverheadCostPerUnit, 2)
            : 0;

        return new BillOfMaterialsDto(
            b.Id,
            b.LegalEntityId,
            b.Code,
            b.Name,
            b.ProducedItemId,
            b.ProducedItemCode,
            b.ProducedItemName,
            b.OutputQuantity,
            b.Uom,
            b.Version,
            b.LaborCostPerUnit,
            b.OverheadCostPerUnit,
            totalMaterial,
            totalUnitCost,
            b.IsActive,
            b.Description,
            b.Components.Count,
            b.Components.Select(c => new BomComponentDto(
                c.Id,
                c.BomId,
                c.ComponentItemId,
                c.ComponentItemCode,
                c.ComponentItemName,
                c.QuantityRequired,
                c.Uom,
                c.ScrapPercentage,
                c.UnitCost,
                Math.Round(c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)), 2),
                c.Notes,
                c.UseCurrentCost)).ToList(),
            b.CustomFields,
            b.CreatedAt);
    }
}
