using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Modularity;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory;

/// <summary>Stub entity proving that a module owns its own tables (table prefix "inv_").</summary>
[DefaultSort("code")]
public class Warehouse : LegalEntityScopedEntity, IHasCustomFields
{
    [Listable] public string Code { get; set; } = default!;
    [Listable] public string Name { get; set; } = default!;
    [Searchable] public string? Address { get; set; }
    [Filterable] public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}

/// <summary>Sample horizontal module (stock). Sprint 0 ships warehouses only, to prove the module mechanism end to end.</summary>
public sealed class InventoryModule : ModuleBase
{
    public const string Key = "inventory";
    public const string WarehousesRead = "inventory.warehouses.read";
    public const string WarehousesWrite = "inventory.warehouses.write";
    public const string StockRead = "inventory.stock.read";
    public const string StockWrite = "inventory.stock.write";
    public const string CountsRead = "inventory.counts.read";
    public const string CountsManage = "inventory.counts.manage";
    public const string ReconciliationRead = "inventory.reconciliation.read";
    public const string AllowNegativeStockSetting = "inventory.allowNegativeStock";

    public override ModuleManifest Manifest { get; } =
        new(Key, "Inventory", "Warehouses, stock on hand, receipts, issues, adjustments, transfers and valuation.", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(WarehousesRead, "View warehouses"),
        PermissionDefinition.Of(WarehousesWrite, "Create and edit warehouses"),
        PermissionDefinition.Of(StockRead, "View stock on hand, valuation and movements"),
        PermissionDefinition.Of(StockWrite, "Receive, issue, adjust and transfer stock; set reorder levels"),
        PermissionDefinition.Of(CountsRead, "View physical stock counts"),
        PermissionDefinition.Of(CountsManage, "Create, enter, post and cancel physical stock counts"),
        PermissionDefinition.Of(ReconciliationRead, "Compare stock value with the inventory account in the ledger"),
    ];

    public override IEnumerable<SettingDefinition> Settings =>
    [
        new(AllowNegativeStockSetting, "Allow deliveries, trips and other documents to take stock below zero", "false", "bool"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("inventory.stock", "Stock on hand", "/inventory/stock", "appstore", StockRead, 5, "inventory", "Inventory", 300),
        new("inventory.movements", "Stock movements", "/inventory/movements", "file-text", StockRead, 8, "inventory", "Inventory", 300),
        new("inventory.counts", "Stock counts", "/inventory/counts", "check-square", CountsRead, 9, "inventory", "Inventory", 300),
        new("inventory.warehouses", "Warehouses", "/inventory/warehouses", "home", WarehousesRead, 10, "inventory", "Inventory", 300),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        [new("inventory.warehouse", "Warehouse", typeof(Warehouse), Key, WarehousesRead)];

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ERP.Application.Dashboard.IDashboardContributor, InventoryDashboard>();
        services.AddScoped<ERP.Application.Dashboard.IOwnerDashboardSection, InventoryOwnerSection>();
        services.AddScoped<ERP.Application.Search.ISearchProvider, InventorySearch>();
        services.AddScoped<ERP.Application.Reminders.IReminderSource, LowStockReminders>();
        services.AddScoped<IStockPostingService, InventoryStockPostingService>();
        services.AddScoped<StockMovementChecks>();
        services.AddScoped<StockCountDtoBuilder>();
        services.AddScoped<InventoryReportLookups>();
    }

    public override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockMovement>(b =>
        {
            b.ToTable("inv_StockMovements");
            b.Property(x => x.Quantity).HasPrecision(19, 4);
            b.Property(x => x.UnitCost).HasPrecision(19, 4);
            b.Property(x => x.Reference).HasMaxLength(100);
            b.Property(x => x.Memo).HasMaxLength(500);
            b.Property(x => x.ReturnOfReference).HasMaxLength(100);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ERP.Domain.Catalog.Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.WarehouseId, x.ItemId, x.Date });
        });
        modelBuilder.Entity<StockPolicy>(b =>
        {
            b.ToTable("inv_StockPolicies");
            b.Property(x => x.ReorderLevel).HasPrecision(19, 4);
            b.Property(x => x.ReorderQuantity).HasPrecision(19, 4);
            b.HasOne<ERP.Domain.Catalog.Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.OrgId, x.ItemId }).IsUnique();
        });
        modelBuilder.Entity<StockCount>(b =>
        {
            b.ToTable("inv_StockCounts");
            b.Property(x => x.Number).HasMaxLength(30).IsRequired();
            b.Property(x => x.Memo).HasMaxLength(500);
            b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            b.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.StockCountId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OrgId, x.Number }).IsUnique();
            b.HasIndex(x => new { x.OrgId, x.WarehouseId, x.CountDate });
        });
        modelBuilder.Entity<StockCountLine>(b =>
        {
            b.ToTable("inv_StockCountLines");
            b.Property(x => x.SystemQuantity).HasPrecision(19, 4);
            b.Property(x => x.CountedQuantity).HasPrecision(19, 4);
            b.HasOne<ERP.Domain.Catalog.Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.StockCountId, x.ItemId }).IsUnique();
        });
        modelBuilder.Entity<Warehouse>(b =>
        {
            b.ToTable("inv_Warehouses");
            b.Property(x => x.Code).HasMaxLength(20).IsRequired();
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        });
    }
}
