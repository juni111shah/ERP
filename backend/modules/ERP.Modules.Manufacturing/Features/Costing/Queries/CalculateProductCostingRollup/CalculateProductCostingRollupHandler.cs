using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class CalculateProductCostingRollupHandler(IBillOfMaterialsRepository boms, IStockPostingService stock)
    : IQueryHandler<CalculateProductCostingRollupQuery, ProductCostRollupDto?>
{
    public async Task<ProductCostRollupDto?> Handle(CalculateProductCostingRollupQuery request, CancellationToken cancellationToken)
    {
        // 1. Load the BOM.
        var bom = await boms.FindWithComponentsAsync(request.BomId, cancellationToken);

        if (bom == null) return null;

        // 2. Material at current cost, plus labour and overhead for the output.
        var costs = await BomStructure.CurrentCostsAsync(bom.Components, stock, request.WarehouseId, cancellationToken);
        var totalMaterial = BomStructure.MaterialCost(bom.Components, costs);
        var totalCost = totalMaterial + (bom.LaborCostPerUnit * bom.OutputQuantity) + (bom.OverheadCostPerUnit * bom.OutputQuantity);
        var unitCost = bom.OutputQuantity > 0 ? Math.Round(totalCost / bom.OutputQuantity, 2) : 0;

        // 3. Each component's share of the material.
        var breakdown = bom.Components.Select(c =>
        {
            var lineCost = Math.Round(c.QuantityRequired * costs[c.Id] * (1 + (c.ScrapPercentage / 100m)), 2);
            var share = totalMaterial > 0 ? Math.Round((lineCost / totalMaterial) * 100, 1) : 0;
            return new CostComponentBreakdownDto(
                c.ComponentItemId,
                c.ComponentItemCode,
                c.ComponentItemName,
                c.QuantityRequired,
                costs[c.Id],
                lineCost,
                share);
        }).ToList();

        return new ProductCostRollupDto(
            bom.ProducedItemId,
            bom.ProducedItemCode,
            bom.ProducedItemName,
            bom.OutputQuantity,
            totalMaterial,
            bom.LaborCostPerUnit * bom.OutputQuantity,
            bom.OverheadCostPerUnit * bom.OutputQuantity,
            unitCost,
            breakdown);
    }
}
