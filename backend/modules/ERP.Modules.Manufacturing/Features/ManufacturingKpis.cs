using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

public record ManufacturingKpiSummaryDto(
    int ActiveBomsCount,
    int OpenWorkOrdersCount,
    int CompletedWorkOrdersMonth,
    decimal TotalProducedQuantityMonth,
    decimal AverageYieldRatePercentage,
    decimal TotalManufacturingCostMonth);

public record GetManufacturingKpiSummaryQuery : IQuery<ManufacturingKpiSummaryDto>;

public class ManufacturingKpiHandlers : IRequestHandler<GetManufacturingKpiSummaryQuery, ManufacturingKpiSummaryDto>
{
    private readonly IRepository<BillOfMaterials> _boms;
    private readonly IRepository<WorkOrder> _workOrders;

    public ManufacturingKpiHandlers(
        IRepository<BillOfMaterials> boms,
        IRepository<WorkOrder> workOrders)
    {
        _boms = boms;
        _workOrders = workOrders;
    }

    public async Task<ManufacturingKpiSummaryDto> Handle(GetManufacturingKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        var activeBoms = await _boms.Query(new Specification<BillOfMaterials>(b => b.IsActive)).CountAsync(cancellationToken);

        var openOrders = await _workOrders.Query(new Specification<WorkOrder>(w =>
            w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)).CountAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        var completedThisMonth = await _workOrders.Query(new Specification<WorkOrder>(w =>
            w.Status == WorkOrderStatus.Completed && w.CompletionDate >= firstDayOfMonth)).ToListAsync(cancellationToken);

        var completedCount = completedThisMonth.Count;
        var totalProduced = completedThisMonth.Sum(w => w.CompletedQuantity);
        var totalScrapped = completedThisMonth.Sum(w => w.ScrappedQuantity);
        var totalCost = completedThisMonth.Sum(w => w.ActualTotalCost);

        var totalQty = totalProduced + totalScrapped;
        var yieldRate = totalQty > 0 ? Math.Round((totalProduced / totalQty) * 100, 1) : 100m;

        return new ManufacturingKpiSummaryDto(
            activeBoms,
            openOrders,
            completedCount,
            totalProduced,
            yieldRate,
            totalCost);
    }
}
