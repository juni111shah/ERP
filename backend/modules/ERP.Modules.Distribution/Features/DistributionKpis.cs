using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Distribution.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Features;

public record DistributionKpiSummaryDto(
    int TotalRoutes,
    int ActiveRoutes,
    int ActiveTripsToday,
    int CompletedTripsMonth,
    decimal TotalVanSalesMonth,
    decimal TotalCollectionsMonth,
    int WholesaleRulesCount);

public record GetDistributionKpiSummaryQuery : IQuery<DistributionKpiSummaryDto>;

public class DistributionKpiHandlers : IRequestHandler<GetDistributionKpiSummaryQuery, DistributionKpiSummaryDto>
{
    private readonly IRepository<DistributionRoute> _routes;
    private readonly IRepository<VanSalesTrip> _trips;
    private readonly IRepository<WholesalePriceMatrix> _wholesale;

    public DistributionKpiHandlers(
        IRepository<DistributionRoute> routes,
        IRepository<VanSalesTrip> trips,
        IRepository<WholesalePriceMatrix> wholesale)
    {
        _routes = routes;
        _trips = trips;
        _wholesale = wholesale;
    }

    public async Task<DistributionKpiSummaryDto> Handle(GetDistributionKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        var totalRoutes = await _routes.Query().CountAsync(cancellationToken);
        var activeRoutes = await _routes.Query(new Specification<DistributionRoute>(r => r.IsActive)).CountAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstDayOfMonth = new DateOnly(today.Year, today.Month, 1);

        var activeTripsToday = await _trips.Query(new Specification<VanSalesTrip>(t =>
            t.Status == TripStatus.InProgress && t.DepartureDate == today)).CountAsync(cancellationToken);

        var completedTrips = await _trips.Query(new Specification<VanSalesTrip>(t =>
            t.Status == TripStatus.Completed && t.DepartureDate >= firstDayOfMonth)).ToListAsync(cancellationToken);

        var totalSales = completedTrips.Sum(t => t.TotalSalesAmount);
        var totalCollections = completedTrips.Sum(t => t.TotalCollectionsAmount);

        var wholesaleCount = await _wholesale.Query(new Specification<WholesalePriceMatrix>(w => w.IsActive)).CountAsync(cancellationToken);

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
