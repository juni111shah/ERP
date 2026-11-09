using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class GetDistributionKpiSummaryHandler(
    IDistributionRouteRepository routes,
    IVanSalesTripRepository trips,
    IWholesalePriceMatrixRepository wholesale)
    : IQueryHandler<GetDistributionKpiSummaryQuery, DistributionKpiSummaryDto>
{
    public async Task<DistributionKpiSummaryDto> Handle(GetDistributionKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. Routes.
        var totalRoutes = await routes.CountAllAsync(cancellationToken);
        var activeRoutes = await routes.CountActiveAsync(cancellationToken);

        // 2. Trips out today and completed this month.
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        var activeTripsToday = await trips.CountInProgressDepartedOnAsync(today, cancellationToken);

        var completedTrips = await trips.ListCompletedDepartedSinceAsync(firstDayOfMonth, cancellationToken);

        var totalSales = completedTrips.Sum(t => t.TotalSalesAmount);
        var totalCollections = completedTrips.Sum(t => t.TotalCollectionsAmount);

        // 3. Wholesale rules.
        var wholesaleCount = await wholesale.CountActiveAsync(cancellationToken);

        return new DistributionKpiSummaryDto(
            totalRoutes,
            activeRoutes,
            activeTripsToday,
            completedTrips.Count,
            totalSales,
            totalCollections,
            wholesaleCount);
    }
}
