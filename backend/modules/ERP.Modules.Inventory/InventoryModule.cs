using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Modularity;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

public sealed record WarehouseInput(string Code, string Name, Guid LegalEntityId, string? Address);
public sealed record CountedLinesBody(List<CountedLineInput> Lines);

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
        services.AddScoped<ERP.Application.Search.ISearchProvider, InventorySearch>();
        services.AddScoped<ERP.Application.Reminders.IReminderSource, LowStockReminders>();
        services.AddScoped<IStockPostingService, InventoryStockPostingService>();
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

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        // One line: paged list with search / filter / sort from the [Listable]/[Filterable] attributes.
        group.MapPagedList<Warehouse, object>("/warehouses", WarehousesRead, "inventory.warehouse",
            (w, cf) => new { w.Id, w.Code, w.Name, w.LegalEntityId, w.Address, w.IsActive, customFields = cf });

        group.MapPagedList<StockMovement, MovementDto>("/movements", StockRead, m => new MovementDto(m.Id, m.WarehouseId, m.ItemId, m.Date, m.Kind, m.Quantity, m.UnitCost, m.Reference, m.Memo, m.JournalEntryId));
        group.MapGet("/stock", (Guid? warehouseId, Guid? itemId, ISender s, CancellationToken ct) => s.Send(new GetStockOnHand(warehouseId, itemId), ct)).RequirePermission(StockRead);
        group.MapGet("/stock/low", (ISender s, CancellationToken ct) => s.Send(new GetLowStock(), ct)).RequirePermission(StockRead);
        group.MapPost("/stock/receive", async (ReceiveStockInput i, ISender s, CancellationToken ct) => Results.Created("/api/v1/m/inventory/movements", await s.Send(new ReceiveStock(i), ct))).RequirePermission(StockWrite);
        group.MapPost("/stock/issue", async (IssueStockInput i, ISender s, CancellationToken ct) => Results.Created("/api/v1/m/inventory/movements", await s.Send(new IssueStock(i), ct))).RequirePermission(StockWrite);
        group.MapPost("/stock/adjust", async (AdjustStockInput i, ISender s, CancellationToken ct) => Results.Created("/api/v1/m/inventory/movements", await s.Send(new AdjustStock(i), ct))).RequirePermission(StockWrite);
        group.MapPost("/stock/transfer", async (TransferStockInput i, ISender s, CancellationToken ct) => Results.Created("/api/v1/m/inventory/movements", await s.Send(new TransferStock(i), ct))).RequirePermission(StockWrite);
        group.MapPut("/stock/policy", async (StockPolicyInput i, ISender s, CancellationToken ct) => { await s.Send(new SetStockPolicy(i), ct); return Results.NoContent(); }).RequirePermission(StockWrite);
        group.MapPut("/warehouses/{id:guid}", (Guid id, WarehouseUpdateInput input, ISender s, CancellationToken ct) => s.Send(new UpdateWarehouse(id, input), ct)).RequirePermission(WarehousesWrite);

        var counts = group.MapGroup("/counts");
        counts.MapPagedList<StockCount, StockCountRow>("", CountsRead, c => new StockCountRow(c.Id, c.Number, c.WarehouseId, c.CountDate, c.Status, c.Lines.Count, c.Memo));
        counts.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetStockCount(id), ct)).RequirePermission(CountsRead);
        counts.MapPost("", async (StockCountInput i, ISender s, CancellationToken ct) => { var c = await s.Send(new CreateStockCount(i), ct); return Results.Created($"/api/v1/m/inventory/counts/{c.Id}", c); }).RequirePermission(CountsManage);
        counts.MapPut("/{id:guid}/lines", (Guid id, CountedLinesBody body, ISender s, CancellationToken ct) => s.Send(new SaveCountedQuantities(id, body.Lines), ct)).RequirePermission(CountsManage);
        counts.MapPost("/{id:guid}/post", (Guid id, ISender s, CancellationToken ct) => s.Send(new PostStockCount(id), ct)).RequirePermission(CountsManage);
        counts.MapPost("/{id:guid}/cancel", (Guid id, ISender s, CancellationToken ct) => s.Send(new CancelStockCount(id), ct)).RequirePermission(CountsManage);

        var reports = group.MapGroup("/reports");
        reports.MapGet("/valuation", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetStockValuation(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow)), ct)).RequirePermission(StockRead);
        reports.MapGet("/movement-summary", (DateOnly from, DateOnly to, ISender s, CancellationToken ct) => s.Send(new GetMovementSummary(from, to), ct)).RequirePermission(StockRead);
        reports.MapGet("/slow-moving", (DateOnly? asOf, int? days, ISender s, CancellationToken ct) => s.Send(new GetSlowMoving(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow), days ?? 90), ct)).RequirePermission(StockRead);
        reports.MapGet("/reorder", (ISender s, CancellationToken ct) => s.Send(new GetReorderSuggestions(), ct)).RequirePermission(StockRead);

        group.MapPost("/warehouses", async (WarehouseInput input, ISender sender, CancellationToken ct) =>
        {
            var created = await sender.Send(new CreateWarehouse(input), ct);
            return Results.Created($"/api/v1/m/inventory/warehouses/{created.Id}", created);
        }).RequirePermission(WarehousesWrite);
    }
}
