using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

public record CalculateProductCostingRollupQuery(Guid BomId) : IQuery<ProductCostRollupDto?>;

public record GetProductCostVarianceQuery(Guid LegalEntityId, Guid ItemId) : IQuery<ProductCostVarianceDto?>;

public class CostingHandlers :
    IRequestHandler<CalculateProductCostingRollupQuery, ProductCostRollupDto?>,
    IRequestHandler<GetProductCostVarianceQuery, ProductCostVarianceDto?>
{
    private readonly IRepository<BillOfMaterials> _boms;
    private readonly IRepository<WorkOrder> _workOrders;

    public CostingHandlers(
        IRepository<BillOfMaterials> boms,
        IRepository<WorkOrder> workOrders)
    {
        _boms = boms;
        _workOrders = workOrders;
    }

    public async Task<ProductCostRollupDto?> Handle(CalculateProductCostingRollupQuery request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.BomId))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken);

        if (bom == null) return null;

        var totalMaterial = bom.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)));
        var totalCost = totalMaterial + (bom.LaborCostPerUnit * bom.OutputQuantity) + (bom.OverheadCostPerUnit * bom.OutputQuantity);
        var unitCost = bom.OutputQuantity > 0 ? Math.Round(totalCost / bom.OutputQuantity, 2) : 0;

        var breakdown = bom.Components.Select(c =>
        {
            var lineCost = Math.Round(c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)), 2);
            var share = totalMaterial > 0 ? Math.Round((lineCost / totalMaterial) * 100, 1) : 0;
            return new CostComponentBreakdownDto(
                c.ComponentItemId,
                c.ComponentItemCode,
                c.ComponentItemName,
                c.QuantityRequired,
                c.UnitCost,
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

    public async Task<ProductCostVarianceDto?> Handle(GetProductCostVarianceQuery request, CancellationToken cancellationToken)
    {
        var activeBom = await _boms.Query(new Specification<BillOfMaterials>(b =>
            b.LegalEntityId == request.LegalEntityId && b.ProducedItemId == request.ItemId && b.IsActive))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken);

        var completedOrders = await _workOrders.Query(new Specification<WorkOrder>(w =>
            w.LegalEntityId == request.LegalEntityId &&
            w.ProducedItemId == request.ItemId &&
            w.Status == WorkOrderStatus.Completed &&
            w.CompletedQuantity > 0))
            .ToListAsync(cancellationToken);

        if (activeBom == null && completedOrders.Count == 0) return null;

        decimal standardUnitCost = 0;
        if (activeBom != null)
        {
            var mat = activeBom.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)));
            standardUnitCost = activeBom.OutputQuantity > 0
                ? Math.Round((mat / activeBom.OutputQuantity) + activeBom.LaborCostPerUnit + activeBom.OverheadCostPerUnit, 2)
                : 0;
        }

        decimal actualAvgUnitCost = 0;
        if (completedOrders.Count > 0)
        {
            var totalQty = completedOrders.Sum(w => w.CompletedQuantity);
            var totalActualCost = completedOrders.Sum(w => w.ActualTotalCost);
            actualAvgUnitCost = totalQty > 0 ? Math.Round(totalActualCost / totalQty, 2) : 0;
        }

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
