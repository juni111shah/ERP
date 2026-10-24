namespace ERP.Modules.Distribution;

public static class Permissions
{
    public const string RoutesRead = "distribution.routes.read";
    public const string RoutesManage = "distribution.routes.manage";

    public const string TripsRead = "distribution.trips.read";
    public const string TripsManage = "distribution.trips.manage";

    public const string PricingRead = "distribution.pricing.read";
    public const string PricingManage = "distribution.pricing.manage";

    public const string AnalyticsRead = "distribution.analytics.read";

    public static readonly string[] All =
    [
        RoutesRead,
        RoutesManage,
        TripsRead,
        TripsManage,
        PricingRead,
        PricingManage,
        AnalyticsRead
    ];
}
