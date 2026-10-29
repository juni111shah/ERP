namespace ERP.Modules.Manufacturing;

public static class Permissions
{
    public const string BomsRead = "manufacturing.boms.read";
    public const string BomsManage = "manufacturing.boms.manage";

    public const string WorkOrdersRead = "manufacturing.workorders.read";
    public const string WorkOrdersManage = "manufacturing.workorders.manage";

    public const string CostingRead = "manufacturing.costing.read";
    public const string AnalyticsRead = "manufacturing.analytics.read";

    public static readonly string[] All =
    [
        BomsRead,
        BomsManage,
        WorkOrdersRead,
        WorkOrdersManage,
        CostingRead,
        AnalyticsRead
    ];
}
