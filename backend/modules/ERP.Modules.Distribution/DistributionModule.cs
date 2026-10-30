using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Distribution.Domain;
using ERP.Modules.Distribution.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Distribution;

public sealed class DistributionModule : ModuleBase
{
    public const string Key = "distribution";

    public override ModuleManifest Manifest { get; } = new(Key, "Distribution", "Routes, van sales and wholesale pricing.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(Distribution.Permissions.RoutesRead, "View delivery routes, stops and schedules"),
        PermissionDefinition.Of(Distribution.Permissions.RoutesManage, "Create and modify delivery routes and stops"),
        PermissionDefinition.Of(Distribution.Permissions.TripsRead, "View van sales trips, dispatch sheets and inventory"),
        PermissionDefinition.Of(Distribution.Permissions.TripsManage, "Dispatch, complete and reconcile van sales trips"),
        PermissionDefinition.Of(Distribution.Permissions.PricingRead, "View wholesale price lists and tiered discount matrices"),
        PermissionDefinition.Of(Distribution.Permissions.PricingManage, "Manage wholesale pricing tiers and volume rules"),
        PermissionDefinition.Of(Distribution.Permissions.AnalyticsRead, "View distribution fleet performance and sales analytics"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("distribution.routes", "Routes", "/distribution/routes", "compass", Distribution.Permissions.RoutesRead, 10, "distribution", "Distribution", 340),
        new("distribution.trips", "Van sales", "/distribution/trips", "car", Distribution.Permissions.TripsRead, 20, "distribution", "Distribution", 340),
        new("distribution.wholesale", "Wholesale pricing", "/distribution/wholesale", "percentage", Distribution.Permissions.PricingRead, 30, "distribution", "Distribution", 340),
        new("distribution.analytics", "Distribution analytics", "/distribution/analytics", "dashboard", Distribution.Permissions.AnalyticsRead, 40, "distribution", "Distribution", 340),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("distribution.autoCreateDeliveryOrders", "Automatically convert completed trip sales to invoices", "true", "bool"),
        new("distribution.strictInventoryLoading", "Prevent dispatching trips with stock exceeding vehicle capacity", "true", "bool"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new("DistributionRoute", "Distribution route", typeof(DistributionRoute), Key, Distribution.Permissions.RoutesRead),
        new("VanSalesTrip", "Van sales trip", typeof(VanSalesTrip), Key, Distribution.Permissions.TripsRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("fmcg_distribution", "FMCG & Wholesale Distribution", "Van sales routes, trip cash collections and wholesale pricing.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Van Sales Driver", "Trip dispatch, van inventory receipt, counter sale execution",
                    [Distribution.Permissions.RoutesRead, Distribution.Permissions.TripsRead, Distribution.Permissions.TripsManage], Key),
                new("Distribution Operations Manager", "Full fleet route control, driver scheduling, wholesale tiered pricing",
                    [Distribution.Permissions.RoutesRead, Distribution.Permissions.RoutesManage, Distribution.Permissions.TripsRead, Distribution.Permissions.TripsManage,
                     Distribution.Permissions.PricingRead, Distribution.Permissions.PricingManage, Distribution.Permissions.AnalyticsRead], Key),
            ],
            Sequences:
            [
                new("VanSalesTrip", "TRIP", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 50),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, DistributionDashboard>();
        services.AddScoped<ISearchProvider, DistributionSearch>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<DistributionRoute>(b =>
        {
            b.ToTable("dst_Routes");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.DriverName).HasMaxLength(150);
            b.Property(x => x.VehicleNumber).HasMaxLength(50);
            b.Property(x => x.VehicleCapacityKg).HasPrecision(18, 4);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasMany(x => x.Stops).WithOne(s => s.Route).HasForeignKey(s => s.RouteId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<DeliveryStop>(b =>
        {
            b.ToTable("dst_DeliveryStops");
            b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.ContactPhone).HasMaxLength(50);
            b.Property(x => x.PreferredTimeWindow).HasMaxLength(100);
            b.Property(x => x.SpecialInstructions).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.RouteId });
        });

        mb.Entity<VanSalesTrip>(b =>
        {
            b.ToTable("dst_VanSalesTrips");
            b.Property(x => x.TripNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.RouteName).HasMaxLength(150).IsRequired();
            b.Property(x => x.DriverName).HasMaxLength(150).IsRequired();
            b.Property(x => x.VehicleNumber).HasMaxLength(50).IsRequired();
            b.Property(x => x.TotalSalesAmount).HasPrecision(19, 4);
            b.Property(x => x.TotalCollectionsAmount).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrgId, x.TripNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasMany(x => x.InventoryItems).WithOne(i => i.Trip).HasForeignKey(i => i.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<TripInventoryItem>(b =>
        {
            b.ToTable("dst_TripInventoryItems");
            b.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Uom).HasMaxLength(20).IsRequired();
            b.Property(x => x.LoadedQuantity).HasPrecision(18, 4);
            b.Property(x => x.SoldQuantity).HasPrecision(18, 4);
            b.Property(x => x.ReturnedQuantity).HasPrecision(18, 4);
            b.Property(x => x.DamagedQuantity).HasPrecision(18, 4);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.TripId });
        });

        mb.Entity<WholesalePriceMatrix>(b =>
        {
            b.ToTable("dst_WholesalePrices");
            b.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.MinQuantity).HasPrecision(18, 4);
            b.Property(x => x.MaxQuantity).HasPrecision(18, 4);
            b.Property(x => x.DiscountPercentage).HasPrecision(18, 4);
            b.Property(x => x.WholesaleUnitPrice).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.ItemId, x.Tier, x.MinQuantity });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => DistributionEndpoints.Map(group);
}
