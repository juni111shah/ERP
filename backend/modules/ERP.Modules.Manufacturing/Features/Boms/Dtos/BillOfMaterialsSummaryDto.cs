using System.Linq.Expressions;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Features;

public record BillOfMaterialsSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string Code,
    string Name,
    string ProducedItemCode,
    string ProducedItemName,
    decimal OutputQuantity,
    string Uom,
    string Version,
    decimal TotalUnitCost,
    bool IsActive,
    int ComponentCount,
    DateTimeOffset CreatedAt)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<BillOfMaterials, BillOfMaterialsSummaryDto>> Projection = b => new BillOfMaterialsSummaryDto(
        b.Id, b.LegalEntityId, b.Code, b.Name, b.ProducedItemCode, b.ProducedItemName,
        b.OutputQuantity, b.Uom, b.Version,
        b.OutputQuantity > 0 ? ((b.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m))) / b.OutputQuantity) + b.LaborCostPerUnit + b.OverheadCostPerUnit) : 0,
        b.IsActive, b.Components.Count, b.CreatedAt);
}
