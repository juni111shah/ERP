using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class GetProductCostVarianceHandler(IBillOfMaterialsRepository boms, IWorkOrderRepository workOrders, IStockPostingService stock)
    : IQueryHandler<GetProductCostVarianceQuery, ProductCostVarianceDto?>
{
    public async Task<ProductCostVarianceDto?> Handle(GetProductCostVarianceQuery request, CancellationToken cancellationToken)
    {
        // 1. The active BOM and the completed orders of the item.
        var activeBom = await boms.FindActiveForItemWithComponentsAsync(request.LegalEntityId, request.ItemId, cancellationToken);

        var completedOrders = await workOrders.ListCompletedForItemAsync(request.LegalEntityId, request.ItemId, cancellationToken);

        if (activeBom == null && completedOrders.Count == 0) return null;

        // 2. Standard: the active BOM at current cost.
        decimal standardUnitCost = 0;
        if (activeBom != null)
        {
            var costs = await BomStructure.CurrentCostsAsync(activeBom.Components, stock, null, cancellationToken);
            var mat = BomStructure.MaterialCost(activeBom.Components, costs);
            standardUnitCost = activeBom.OutputQuantity > 0
                ? Math.Round((mat / activeBom.OutputQuantity) + activeBom.LaborCostPerUnit + activeBom.OverheadCostPerUnit, 2)
                : 0;
        }

        // 3. Actual: the average over what the completed orders made.
        decimal actualAvgUnitCost = 0;
        if (completedOrders.Count > 0)
        {
            var totalQty = completedOrders.Sum(w => w.CompletedQuantity);
            var totalActualCost = completedOrders.Sum(w => w.ActualTotalCost);
            actualAvgUnitCost = totalQty > 0 ? Math.Round(totalActualCost / totalQty, 2) : 0;
        }

        // 4. The variance.
        var variance = Math.Round(actualAvgUnitCost - standardUnitCost, 2);
        var variancePct = standardUnitCost > 0 ? Math.Round((variance / standardUnitCost) * 100, 1) : 0;

        var itemCode = activeBom?.ProducedItemCode ?? completedOrders.FirstOrDefault()?.ProducedItemCode ?? string.Empty;
        var itemName = activeBom?.ProducedItemName ?? completedOrders.FirstOrDefault()?.ProducedItemName ?? string.Empty;

        return new ProductCostVarianceDto(
            request.ItemId,
            itemCode,
            itemName,
            standardUnitCost,
            actualAvgUnitCost,
            variance,
            variancePct,
            completedOrders.Count);
    }
}
