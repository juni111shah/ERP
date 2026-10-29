using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Sales;

public sealed class SalesModule : ModuleBase
{
    public const string Key = "sales";

    public override ModuleManifest Manifest { get; } = new(Key, "Sales", "Quotes, sales orders, invoices and receipts.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(P.QuotesRead, "View sales quotes"),
        PermissionDefinition.Of(P.QuotesManage, "Create, edit, and convert sales quotes"),
        PermissionDefinition.Of(P.OrdersRead, "View sales orders"),
        PermissionDefinition.Of(P.OrdersManage, "Create, edit, fulfill, and invoice sales orders"),
        PermissionDefinition.Of(P.AnalyticsRead, "View sales analytics and KPI reporting"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("sales.quotes", "Quotes", "/sales/quotes", "file-text", P.QuotesRead, 10, "sales", "Sales", 300),
        new("sales.orders", "Sales orders", "/sales/orders", "shopping-cart", P.OrdersRead, 20, "sales", "Sales", 300),
        new("sales.analytics", "Sales analytics", "/sales/analytics", "line-chart", P.AnalyticsRead, 30, "sales", "Sales", 300),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("sales.defaultQuoteValidityDays", "Default quote validity in days", "30", "number"),
        new("sales.autoConfirmOrdersFromQuotes", "Automatically mark converted orders as confirmed", "true", "bool"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new(Docs.Quote, "Sales quote", typeof(SalesQuote), Key, P.QuotesRead),
        new(Docs.SalesOrder, "Sales order", typeof(SalesOrder), Key, P.OrdersRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("trading", "Wholesale & Trading", "Quotes, order processing and commercial sales.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Sales manager", "Quotes, orders, and sales performance",
                    [P.QuotesRead, P.QuotesManage, P.OrdersRead, P.OrdersManage, P.AnalyticsRead, CorePermissions.InvoicesRead, CorePermissions.InvoicesWrite], Key),
            ],
            Sequences:
            [
                new(Docs.Quote, "QT", "{prefix}-{seq:4}", false),
                new(Docs.SalesOrder, "SO", "{prefix}-{seq:4}", false),
            ],
            SortOrder: 30),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, SalesDashboard>();
        services.AddScoped<ISearchProvider, SalesSearch>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<SalesQuote>(b =>
        {
            b.ToTable("sls_Quotes");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CustomerEmail).HasMaxLength(150);
            b.Property(x => x.CustomerPhone).HasMaxLength(50);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.SubTotal).HasPrecision(19, 4);
            b.Property(x => x.TaxTotal).HasPrecision(19, 4);
            b.Property(x => x.DiscountTotal).HasPrecision(19, 4);
            b.Property(x => x.TotalAmount).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.Property(x => x.Terms).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.QuoteId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<SalesQuoteLine>(b =>
        {
            b.ToTable("sls_QuoteLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.Property(x => x.TaxRate).HasPrecision(5, 2);
            b.Property(x => x.LineTotal).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.QuoteId });
        });

        mb.Entity<SalesOrder>(b =>
        {
            b.ToTable("sls_Orders");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.QuoteNumber).HasMaxLength(30);
            b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CustomerReference).HasMaxLength(100);
            b.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            b.Property(x => x.SubTotal).HasPrecision(19, 4);
            b.Property(x => x.TaxTotal).HasPrecision(19, 4);
            b.Property(x => x.DiscountTotal).HasPrecision(19, 4);
            b.Property(x => x.TotalAmount).HasPrecision(19, 4);
            b.Property(x => x.PaymentTerms).HasMaxLength(200);
            b.Property(x => x.ShippingAddress).HasMaxLength(500);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.LegalEntityId, x.Status });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<SalesOrderLine>(b =>
        {
            b.ToTable("sls_OrderLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.DeliveredQuantity).HasPrecision(18, 4);
            b.Property(x => x.InvoicedQuantity).HasPrecision(18, 4);
            b.Property(x => x.UnitOfMeasure).HasMaxLength(20);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.Property(x => x.TaxRate).HasPrecision(5, 2);
            b.Property(x => x.LineTotal).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.OrderId });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => SalesEndpoints.Map(group);
}
