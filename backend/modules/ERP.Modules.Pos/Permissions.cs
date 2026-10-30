namespace ERP.Modules.Pos;

public static class Permissions
{
    public const string RegistersRead = "pos.registers.read";
    public const string RegistersManage = "pos.registers.manage";
    public const string SessionsRead = "pos.sessions.read";
    public const string SessionsManage = "pos.sessions.manage";
    public const string OrdersRead = "pos.orders.read";
    public const string OrdersCreate = "pos.orders.create";
    public const string OrdersVoid = "pos.orders.void";
    public const string AnalyticsRead = "pos.analytics.read";
    public const string OrdersOverridePrice = "pos.orders.override-price";

    public static readonly string[] All =
    [
        RegistersRead,
        RegistersManage,
        SessionsRead,
        SessionsManage,
        OrdersRead,
        OrdersCreate,
        OrdersVoid,
        AnalyticsRead,
        OrdersOverridePrice
    ];
}
