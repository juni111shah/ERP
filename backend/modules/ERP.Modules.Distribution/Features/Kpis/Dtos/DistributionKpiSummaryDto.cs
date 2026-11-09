namespace ERP.Modules.Distribution.Features;

public record DistributionKpiSummaryDto(
    int TotalRoutes,
    int ActiveRoutes,
    int ActiveTripsToday,
    int CompletedTripsMonth,
    decimal TotalVanSalesMonth,
    decimal TotalCollectionsMonth,
    int WholesaleRulesCount);
