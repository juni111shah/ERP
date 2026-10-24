using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Retail.Domain;
using ERP.Modules.Retail.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Retail;

public sealed class RetailModule : ModuleBase
{
    public const string Key = "retail";

    public override ModuleManifest Manifest { get; } = new(Key, "Retail", "Stores, price lists and promotions.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(Retail.Permissions.StoresRead, "View retail stores and branches"),
        PermissionDefinition.Of(Retail.Permissions.StoresManage, "Create and manage retail stores"),
        PermissionDefinition.Of(Retail.Permissions.PriceListsRead, "View price lists and pricing tiers"),
        PermissionDefinition.Of(Retail.Permissions.PriceListsManage, "Create and manage retail price lists"),
        PermissionDefinition.Of(Retail.Permissions.PromotionsRead, "View retail promotions and coupons"),
        PermissionDefinition.Of(Retail.Permissions.PromotionsManage, "Create and manage promotions and discount campaigns"),
        PermissionDefinition.Of(Retail.Permissions.AnalyticsRead, "View retail chain performance analytics"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("retail.stores", "Stores", "/retail/stores", "shop", Retail.Permissions.StoresRead, 10, "retail", "Retail", 330),
        new("retail.pricelists", "Price lists", "/retail/pricelists", "tags", Retail.Permissions.PriceListsRead, 20, "retail", "Retail", 330),
        new("retail.promotions", "Promotions", "/retail/promotions", "gift", Retail.Permissions.PromotionsRead, 30, "retail", "Retail", 330),
        new("retail.analytics", "Retail analytics", "/retail/analytics", "line-chart", Retail.Permissions.AnalyticsRead, 40, "retail", "Retail", 330),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("retail.stackPromotions", "Allow stacking multiple promotional coupons", "false", "bool"),
        new("retail.defaultPriceListType", "Default price list pricing mechanism", "Standard", "string"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new("RetailStore", "Retail store", typeof(RetailStore), Key, Retail.Permissions.StoresRead),
        new("RetailPriceList", "Retail price list", typeof(RetailPriceList), Key, Retail.Permissions.PriceListsRead),
        new("RetailPromotion", "Retail promotion", typeof(RetailPromotion), Key, Retail.Permissions.PromotionsRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("retail_chain", "Multi-Store Retail Chain", "Branch stores, seasonal promotional pricing and discounts.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Store Manager", "Local branch store management and pricing access",
                    [Retail.Permissions.StoresRead, Retail.Permissions.PriceListsRead, Retail.Permissions.PromotionsRead, Retail.Permissions.AnalyticsRead], Key),
                new("Retail Director", "Full authority over store network, price lists and promotions",
                    [Retail.Permissions.StoresRead, Retail.Permissions.StoresManage, Retail.Permissions.PriceListsRead, Retail.Permissions.PriceListsManage,
                     Retail.Permissions.PromotionsRead, Retail.Permissions.PromotionsManage, Retail.Permissions.AnalyticsRead], Key),
            ],
            Sequences:
            [
                new("PROMO", "PRM", "{prefix}-{seq:4}", false),
            ],
            SortOrder: 40),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, RetailDashboard>();
        services.AddScoped<ISearchProvider, RetailSearch>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<RetailStore>(b =>
        {
            b.ToTable("ret_Stores");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.StoreManagerName).HasMaxLength(150);
            b.Property(x => x.Phone).HasMaxLength(50);
            b.Property(x => x.Email).HasMaxLength(100);
            b.Property(x => x.Address).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        mb.Entity<RetailPriceList>(b =>
        {
            b.ToTable("ret_PriceLists");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PriceListId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<RetailPriceListItem>(b =>
        {
            b.ToTable("ret_PriceListItems");
            b.Property(x => x.ItemCode).HasMaxLength(50).IsRequired();
            b.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
            b.Property(x => x.BasePrice).HasPrecision(19, 4);
            b.Property(x => x.Price).HasPrecision(19, 4);
            b.Property(x => x.MinQuantity).HasPrecision(18, 4);
            b.HasIndex(x => new { x.OrgId, x.PriceListId });
        });

        mb.Entity<RetailPromotion>(b =>
        {
            b.ToTable("ret_Promotions");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.DiscountValue).HasPrecision(19, 4);
            b.Property(x => x.MinimumOrderAmount).HasPrecision(19, 4);
            b.Property(x => x.Description).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => RetailEndpoints.Map(group);
}
