using ERP.Application.Common;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class GetManufacturingKpiSummaryHandler(IBillOfMaterialsRepository boms, IWorkOrderRepository workOrders)
    : IQueryHandler<GetManufacturingKpiSummaryQuery, ManufacturingKpiSummaryDto>
{
    public async Task<ManufacturingKpiSummaryDto> Handle(GetManufacturingKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. Counts.
        var activeBoms = await boms.CountActiveAsync(cancellationToken);

        var openOrders = await workOrders.CountOpenAsync(cancellationToken);

        // 2. What was completed this month.
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        var completedThisMonth = await workOrders.ListCompletedSinceAsync(firstDayOfMonth, cancellationToken);

        var completedCount = completedThisMonth.Count;
        var totalProduced = completedThisMonth.Sum(w => w.CompletedQuantity);
        var totalScrapped = completedThisMonth.Sum(w => w.ScrappedQuantity);
        var totalCost = completedThisMonth.Sum(w => w.ActualTotalCost);

        // 3. Yield: good units out of everything worked on.
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
