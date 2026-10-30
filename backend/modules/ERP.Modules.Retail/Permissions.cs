namespace ERP.Modules.Retail;

public static class Permissions
{
    public const string StoresRead = "retail.stores.read";
    public const string StoresManage = "retail.stores.manage";
    public const string PriceListsRead = "retail.pricelists.read";
    public const string PriceListsManage = "retail.pricelists.manage";
    public const string PromotionsRead = "retail.promotions.read";
    public const string PromotionsManage = "retail.promotions.manage";
    public const string AnalyticsRead = "retail.analytics.read";

    public static readonly string[] All =
    [
        StoresRead,
        StoresManage,
        PriceListsRead,
        PriceListsManage,
        PromotionsRead,
        PromotionsManage,
        AnalyticsRead
    ];
}
