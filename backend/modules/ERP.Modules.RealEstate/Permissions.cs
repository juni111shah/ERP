namespace ERP.Modules.RealEstate;

/// <summary>Permission keys (<c>realestate.&lt;resource&gt;.&lt;action&gt;</c>, spec 9.1).</summary>
public static class P
{
    public const string UnitTypesRead = "realestate.unittypes.read";
    public const string UnitTypesManage = "realestate.unittypes.manage";
    public const string PropertiesRead = "realestate.properties.read";
    public const string PropertiesManage = "realestate.properties.manage";
    public const string UnitsRead = "realestate.units.read";
    public const string UnitsManage = "realestate.units.manage";
    public const string UnitsReserve = "realestate.units.reserve";
    public const string LeasesRead = "realestate.leases.read";
    public const string LeasesCreate = "realestate.leases.create";
    public const string LeasesEdit = "realestate.leases.edit";
    public const string LeasesActivate = "realestate.leases.activate";
    public const string LeasesRenew = "realestate.leases.renew";
    public const string LeasesTerminate = "realestate.leases.terminate";
    public const string WorkOrdersRead = "realestate.workorders.read";
    public const string WorkOrdersCreate = "realestate.workorders.create";
    public const string WorkOrdersEdit = "realestate.workorders.edit";
    public const string WorkOrdersAssign = "realestate.workorders.assign";
    public const string WorkOrdersComplete = "realestate.workorders.complete";
    public const string WorkOrdersClose = "realestate.workorders.close";
    public const string WorkOrdersCancel = "realestate.workorders.cancel";
    public const string DealsRead = "realestate.deals.read";
    public const string DealsCreate = "realestate.deals.create";
    public const string DealsEdit = "realestate.deals.edit";
    public const string DealsLose = "realestate.deals.lose";
    public const string BillingRun = "realestate.billing.run";
    public const string SettingsWrite = "realestate.settings.write";
}

/// <summary>Document-number keys (see the industry template's sequences) and custom-field entity types.</summary>
public static class Docs
{
    public const string Property = "realestate.property";
    public const string Lease = "realestate.lease";
    public const string WorkOrder = "realestate.workorder";
    public const string Deal = "realestate.deal";
    public const string Unit = "realestate.unit";
}
