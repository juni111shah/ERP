using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Dashboard;
using ERP.Application.Search;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Modularity;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Pos;

public sealed class PosModule : ModuleBase
{
    public const string Key = "pos";

    public override ModuleManifest Manifest { get; } = new(Key, "Point of Sale", "Counter sales, shifts and cash drawers.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(Pos.Permissions.RegistersRead, "View cash registers"),
        PermissionDefinition.Of(Pos.Permissions.RegistersManage, "Create and manage cash registers"),
        PermissionDefinition.Of(Pos.Permissions.SessionsRead, "View shifts and cash drawer sessions"),
        PermissionDefinition.Of(Pos.Permissions.SessionsManage, "Open, close, and reconcile shifts"),
        PermissionDefinition.Of(Pos.Permissions.OrdersRead, "View counter sales orders"),
        PermissionDefinition.Of(Pos.Permissions.OrdersCreate, "Process counter sales at POS terminal"),
        PermissionDefinition.Of(Pos.Permissions.OrdersVoid, "Void POS counter sales"),
        PermissionDefinition.Of(Pos.Permissions.AnalyticsRead, "View POS sales and shift analytics"),
        PermissionDefinition.Of(Pos.Permissions.OrdersOverridePrice, "Override POS item sales price"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("pos.terminal", "POS Terminal", "/pos/terminal", "shop", Pos.Permissions.OrdersCreate, 10, "pos", "Point of sale", 320),
        new("pos.sessions", "Shifts & Drawers", "/pos/sessions", "schedule", Pos.Permissions.SessionsRead, 20, "pos", "Point of sale", 320),
        new("pos.orders", "Counter Orders", "/pos/orders", "shopping-cart", Pos.Permissions.OrdersRead, 30, "pos", "Point of sale", 320),
        new("pos.registers", "Registers", "/pos/registers", "desktop", Pos.Permissions.RegistersRead, 40, "pos", "Point of sale", 320),
        new("pos.analytics", "POS Analytics", "/pos/analytics", "bar-chart", Pos.Permissions.AnalyticsRead, 50, "pos", "Point of sale", 320),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new("pos.allowNegativeDrawer", "Allow drawer balance to go below zero", "false", "bool"),
        new("pos.enforceFloatAtOpen", "Require cashier to specify opening float cash", "true", "bool"),
        new("pos.maxDiscountPercent", "Maximum discount percent allowed on POS order lines", "100", "number"),
        new("pos.allowPartialPayment", "Allow partial payment on POS orders", "false", "bool"),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
    [
        new("PosRegister", "POS register", typeof(PosRegister), Key, Pos.Permissions.RegistersRead),
        new("PosOrder", "POS counter sale", typeof(PosOrder), Key, Pos.Permissions.OrdersRead),
    ];

    public override IEnumerable<IndustryTemplate> IndustryTemplates =>
    [
        new("retail_pos", "Retail & POS Store", "Cashier terminal and shift reconciliation.", IndustryAvailability.Available, [Key],
            Roles:
            [
                new("Cashier", "Counter sales checkout and shift closing",
                    [Pos.Permissions.OrdersCreate, Pos.Permissions.OrdersRead, Pos.Permissions.SessionsManage, Pos.Permissions.SessionsRead], Key),
                new("POS Store Manager", "Register management, shift audit and voiding",
                    [Pos.Permissions.RegistersRead, Pos.Permissions.RegistersManage, Pos.Permissions.SessionsRead, Pos.Permissions.SessionsManage,
                     Pos.Permissions.OrdersRead, Pos.Permissions.OrdersCreate, Pos.Permissions.OrdersVoid, Pos.Permissions.AnalyticsRead], Key),
            ],
            Sequences:
            [
                new("POS", "POS", "{prefix}-{seq:5}", false),
            ],
            SortOrder: 35),
    ];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IDashboardContributor, PosDashboard>();
        services.AddScoped<ISearchProvider, PosSearch>();
    }

    public override void ConfigureModel(ModelBuilder mb)
    {
        mb.Entity<PosRegister>(b =>
        {
            b.ToTable("pos_Registers");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.Property(x => x.Name).HasMaxLength(150).IsRequired();
            b.Property(x => x.ReceiptHeader).HasMaxLength(500);
            b.Property(x => x.ReceiptFooter).HasMaxLength(500);
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        });

        mb.Entity<PosSession>(b =>
        {
            b.ToTable("pos_Sessions");
            b.Property(x => x.RegisterName).HasMaxLength(150).IsRequired();
            b.Property(x => x.CashierName).HasMaxLength(150).IsRequired();
            b.Property(x => x.OpeningCash).HasPrecision(19, 4);
            b.Property(x => x.TotalSales).HasPrecision(19, 4);
            b.Property(x => x.TotalReturns).HasPrecision(19, 4);
            b.Property(x => x.TotalPayIn).HasPrecision(19, 4);
            b.Property(x => x.TotalPayOut).HasPrecision(19, 4);
            b.Property(x => x.ExpectedCash).HasPrecision(19, 4);
            b.Property(x => x.ActualCash).HasPrecision(19, 4);
            b.Property(x => x.CashDifference).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrgId, x.RegisterId, x.Status });
            b.HasMany(x => x.DrawerTransactions).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<CashDrawerTransaction>(b =>
        {
            b.ToTable("pos_DrawerTransactions");
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.Reason).HasMaxLength(250).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.SessionId });
        });

        mb.Entity<PosOrder>(b =>
        {
            b.ToTable("pos_Orders");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CashierName).HasMaxLength(150).IsRequired();
            b.Property(x => x.SubTotal).HasPrecision(19, 4);
            b.Property(x => x.TaxTotal).HasPrecision(19, 4);
            b.Property(x => x.DiscountTotal).HasPrecision(19, 4);
            b.Property(x => x.TotalAmount).HasPrecision(19, 4);
            b.Property(x => x.Notes).HasMaxLength(1000);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique().HasFilter("[IsDeleted] = 0");
            b.HasIndex(x => new { x.OrgId, x.SessionId });
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PosOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Payments).WithOne().HasForeignKey(x => x.PosOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<PosOrderLine>(b =>
        {
            b.ToTable("pos_OrderLines");
            b.Property(x => x.ItemCode).HasMaxLength(50);
            b.Property(x => x.Description).HasMaxLength(500).IsRequired();
            b.Property(x => x.Quantity).HasPrecision(18, 4);
            b.Property(x => x.UnitPrice).HasPrecision(19, 4);
            b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            b.Property(x => x.TaxRate).HasPrecision(5, 2);
            b.Property(x => x.LineTotal).HasPrecision(19, 4);
            b.HasIndex(x => new { x.OrgId, x.PosOrderId });
        });

        mb.Entity<PosPayment>(b =>
        {
            b.ToTable("pos_Payments");
            b.Property(x => x.Amount).HasPrecision(19, 4);
            b.Property(x => x.TenderedAmount).HasPrecision(19, 4);
            b.Property(x => x.ChangeAmount).HasPrecision(19, 4);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.HasIndex(x => new { x.OrgId, x.PosOrderId });
        });
    }

    public override void MapEndpoints(RouteGroupBuilder group) => PosEndpoints.Map(group);
}
