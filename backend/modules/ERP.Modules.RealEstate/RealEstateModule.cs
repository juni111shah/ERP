using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Modularity;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.RealEstate;

/// <summary>
/// Real estate industry module: portfolio (unit types, properties, units, rent history), leasing (leases with rent schedule,
/// activation, renewal, termination, expiry job), work orders and brokerage deals. Invoicing, receipts, cheques and deposits
/// post through the Finance sprint; country-specific fields (Ejari/Tawtheeq, premise numbers...) come from country packs.
/// </summary>
public sealed class RealEstateModule : ModuleBase
{
    public const string Key = "realestate";

    public override ModuleManifest Manifest { get; } = new(
        Key, "Real estate", "Properties, units, leases, facilities and brokerage.", "Industry", RoutePrefix: "real-estate");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.UnitTypesRead, "View unit types"),
        PermissionDefinition.Of(P.UnitTypesManage, "Create and edit unit types"),
        PermissionDefinition.Of(P.PropertiesRead, "View properties"),
        PermissionDefinition.Of(P.PropertiesManage, "Create and edit properties"),
        PermissionDefinition.Of(P.UnitsRead, "View units"),
        PermissionDefinition.Of(P.UnitsManage, "Create and edit units"),
        PermissionDefinition.Of(P.UnitsReserve, "Reserve and release units"),
        PermissionDefinition.Of(P.LeasesRead, "View leases"),
        PermissionDefinition.Of(P.LeasesCreate, "Create draft leases"),
        PermissionDefinition.Of(P.LeasesEdit, "Edit or cancel draft leases"),
        PermissionDefinition.Of(P.LeasesActivate, "Activate leases and run the expiry job"),
        PermissionDefinition.Of(P.LeasesRenew, "Renew leases"),
        PermissionDefinition.Of(P.LeasesTerminate, "Terminate leases and confirm move-out"),
        PermissionDefinition.Of(P.WorkOrdersRead, "View work orders"),
        PermissionDefinition.Of(P.WorkOrdersCreate, "Create work orders"),
        PermissionDefinition.Of(P.WorkOrdersEdit, "Edit open work orders"),
        PermissionDefinition.Of(P.WorkOrdersAssign, "Assign work orders to a vendor or technician"),
        PermissionDefinition.Of(P.WorkOrdersComplete, "Complete work orders"),
        PermissionDefinition.Of(P.WorkOrdersClose, "Close completed work orders"),
        PermissionDefinition.Of(P.WorkOrdersCancel, "Cancel work orders"),
        PermissionDefinition.Of(P.DealsRead, "View deals"),
        PermissionDefinition.Of(P.DealsCreate, "Create deals"),
        PermissionDefinition.Of(P.DealsEdit, "Edit deals and move them through the pipeline"),
        PermissionDefinition.Of(P.DealsLose, "Mark deals as lost"),
        PermissionDefinition.Of(P.BillingRun, "Run the rent billing and invoice instalments"),
        PermissionDefinition.Of(P.SettingsWrite, "Change real estate settings"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("realestate.properties", "Properties", "/real-estate/properties", "bank", P.PropertiesRead, 10, "realestate", "Real estate", 200),
        new("realestate.units", "Units", "/real-estate/units", "appstore", P.UnitsRead, 20, "realestate", "Real estate", 200),
        new("realestate.unittypes", "Unit types", "/real-estate/unit-types", "cluster", P.UnitTypesRead, 25, "realestate", "Real estate", 200),
        new("realestate.leases", "Leases", "/real-estate/leases", "file-text", P.LeasesRead, 30, "realestate", "Real estate", 200),
        new("realestate.billing", "Rent billing", "/real-estate/billing", "dollar", P.BillingRun, 35, "realestate", "Real estate", 200),
        new("realestate.workorders", "Work orders", "/real-estate/work-orders", "tool", P.WorkOrdersRead, 40, "realestate", "Real estate", 200),
        new("realestate.deals", "Deals", "/real-estate/deals", "fund", P.DealsRead, 50, "realestate", "Real estate", 200),
    ];

    /// <summary>Module settings (not columns on the core organization table).</summary>
    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("realestate.leaseExpiryAlertDays", "Lease expiry alert (days)", "60"),
        new("realestate.chequeDueAlertDays", "Cheque due alert (days)", "7"),
    ];

    /// <summary>Record types that accept custom fields (country packs add their fields to these).</summary>
    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new(Docs.Property, "Property", typeof(Property), Key, P.PropertiesRead),
        new(Docs.Unit, "Unit", typeof(Domain.Unit), Key, P.UnitsRead),
        new(Docs.Lease, "Lease", typeof(Lease), Key, P.LeasesRead),
        new(Docs.WorkOrder, "Work order", typeof(WorkOrder), Key, P.WorkOrdersRead),
        new(Docs.Deal, "Deal", typeof(BrokerageDeal), Key, P.DealsRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("realestate", "Real estate", "Landlords, property managers and brokers.", IndustryAvailability.Available, [Key],
            Accounts:
            [
                new("4010", "Leasing income", AccountType.Income, "realestate.income.leasing", ReportingGroup: "Revenue"),
                new("4200", "Brokerage commission income", AccountType.Income, ReportingGroup: "Revenue"),
                new("2200", "Security deposits held", AccountType.Liability, "realestate.deposits", ReportingGroup: "Current liabilities"),
                new("1600", "Investment property", AccountType.Asset, ReportingGroup: "Non-current assets"),
                new("6500", "Repairs and maintenance", AccountType.Expense, ReportingGroup: "Operating expenses"),
            ],
            Roles:
            [
                new("Leasing manager", "Leases, units and collections",
                    [P.PropertiesRead, P.PropertiesManage, P.UnitTypesRead, P.UnitTypesManage, P.UnitsRead, P.UnitsManage, P.UnitsReserve,
                     P.LeasesRead, P.LeasesCreate, P.LeasesEdit, P.LeasesActivate, P.LeasesRenew, P.LeasesTerminate, P.WorkOrdersRead, P.WorkOrdersCreate, P.BillingRun,
                     CorePermissions.PartiesRead, CorePermissions.PartiesWrite, CorePermissions.ChequesRead, CorePermissions.ChequesWrite,
                     CorePermissions.InvoicesRead, CorePermissions.InvoicesWrite, CorePermissions.InvoicesIssue, CorePermissions.ReceiptsRead, CorePermissions.ReceiptsWrite], Key),
                new("Facilities manager", "Maintenance and work orders",
                    [P.PropertiesRead, P.UnitsRead, P.LeasesRead, P.WorkOrdersRead, P.WorkOrdersCreate, P.WorkOrdersEdit, P.WorkOrdersAssign,
                     P.WorkOrdersComplete, P.WorkOrdersClose, P.WorkOrdersCancel, CorePermissions.PartiesRead], Key),
                new("Brokerage manager", "Deals and commissions",
                    [P.PropertiesRead, P.UnitsRead, P.LeasesRead, P.DealsRead, P.DealsCreate, P.DealsEdit, P.DealsLose, CorePermissions.PartiesRead, CorePermissions.PartiesWrite], Key),
            ],
            Sequences:
            [
                new(Docs.Lease, "LSE"),
                new(Docs.Property, "PROP", "{prefix}-{seq:4}", false),
                new(Docs.WorkOrder, "WO"),
                new(Docs.Deal, "DEAL"),
            ],
            SortOrder: 10),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<LeaseDtoFactory>();

    public override void ConfigureModel(ModelBuilder mb) => RealEstateModel.Configure(mb);

    public override void MapEndpoints(RouteGroupBuilder group) => RealEstateEndpoints.Map(group);
}
