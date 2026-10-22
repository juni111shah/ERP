using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.RealEstate;

/// <summary>
/// Real estate industry module (empty in sprint 0). It contributes permissions, menu, module settings and the
/// "realestate" industry template (roles + chart-of-accounts lines). Entities arrive from sprint 2 onwards.
/// </summary>
public sealed class RealEstateModule : ModuleBase
{
    public const string Key = "realestate";

    public static class P
    {
        public const string PortfolioRead = "realestate.portfolio.read";
        public const string PortfolioWrite = "realestate.portfolio.write";
        public const string LeasingRead = "realestate.leasing.read";
        public const string LeasingWrite = "realestate.leasing.write";
        public const string FacilitiesRead = "realestate.facilities.read";
        public const string FacilitiesWrite = "realestate.facilities.write";
        public const string BrokerageRead = "realestate.brokerage.read";
        public const string BrokerageWrite = "realestate.brokerage.write";
        public const string SettingsWrite = "realestate.settings.write";
    }

    public override ModuleManifest Manifest { get; } = new(
        Key, "Real estate", "Properties, units, leases, facilities and brokerage.", "Industry", RoutePrefix: "real-estate");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.PortfolioRead, "View properties and units"),
        PermissionDefinition.Of(P.PortfolioWrite, "Edit properties and units"),
        PermissionDefinition.Of(P.LeasingRead, "View leases"),
        PermissionDefinition.Of(P.LeasingWrite, "Create and manage leases"),
        PermissionDefinition.Of(P.FacilitiesRead, "View work orders"),
        PermissionDefinition.Of(P.FacilitiesWrite, "Manage work orders"),
        PermissionDefinition.Of(P.BrokerageRead, "View deals"),
        PermissionDefinition.Of(P.BrokerageWrite, "Manage deals and commissions"),
        PermissionDefinition.Of(P.SettingsWrite, "Change real estate settings"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("realestate.properties", "Properties", "/real-estate/properties", "bank", P.PortfolioRead, 10, "realestate", "Real estate", 200),
        new("realestate.units", "Units", "/real-estate/units", "appstore", P.PortfolioRead, 20, "realestate", "Real estate", 200),
        new("realestate.leases", "Leases", "/real-estate/leases", "file-text", P.LeasingRead, 30, "realestate", "Real estate", 200),
        new("realestate.workorders", "Work orders", "/real-estate/work-orders", "tool", P.FacilitiesRead, 40, "realestate", "Real estate", 200),
        new("realestate.deals", "Deals", "/real-estate/deals", "fund", P.BrokerageRead, 50, "realestate", "Real estate", 200),
    ];

    /// <summary>Module settings (not columns on the core organization table).</summary>
    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("realestate.leaseExpiryAlertDays", "Lease expiry alert (days)", "60"),
        new("realestate.chequeDueAlertDays", "Cheque due alert (days)", "7"),
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
                new("Leasing manager", "Leases and collections", [P.PortfolioRead, P.PortfolioWrite, P.LeasingRead, P.LeasingWrite, P.FacilitiesRead, P.FacilitiesWrite, CorePermissions.PartiesRead, CorePermissions.PartiesWrite, CorePermissions.ChequesRead, CorePermissions.ChequesWrite], Key),
                new("Facilities manager", "Maintenance and work orders", [P.PortfolioRead, P.FacilitiesRead, P.FacilitiesWrite, P.LeasingRead, CorePermissions.PartiesRead], Key),
                new("Brokerage manager", "Deals and commissions", [P.PortfolioRead, P.BrokerageRead, P.BrokerageWrite, P.LeasingRead, CorePermissions.PartiesRead], Key),
            ],
            Sequences: [new("realestate.lease", "LSE"), new("realestate.property", "PROP", "{prefix}-{seq:4}", false)],
            SortOrder: 10),
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/status", () => Results.Ok(new { module = Key, sprint = 0, message = "Real estate module is installed and enabled." }))
            .RequirePermission(P.PortfolioRead);
    }
}
