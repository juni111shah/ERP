using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Common;
using ERP.Modularity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory;

/// <summary>Stub entity proving that a module owns its own tables (table prefix "inv_").</summary>
public class Warehouse : LegalEntityScopedEntity, IHasCustomFields
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CustomFields { get; set; }
}

public sealed record WarehouseInput(string Code, string Name, Guid LegalEntityId, string? Address);

/// <summary>Sample horizontal module (stock). Sprint 0 ships warehouses only, to prove the module mechanism end to end.</summary>
public sealed class InventoryModule : ModuleBase
{
    public const string Key = "inventory";
    public const string WarehousesRead = "inventory.warehouses.read";
    public const string WarehousesWrite = "inventory.warehouses.write";

    public override ModuleManifest Manifest { get; } =
        new(Key, "Inventory", "Warehouses, stock items and movements (sprint 0: warehouses).", "Horizontal");

    public override IEnumerable<PermissionDefinition> Permissions =>
    [
        PermissionDefinition.Of(WarehousesRead, "View warehouses"),
        PermissionDefinition.Of(WarehousesWrite, "Create and edit warehouses"),
    ];

    public override IEnumerable<MenuItemDefinition> Menu =>
    [
        new("inventory.warehouses", "Warehouses", "/inventory/warehouses", "home", WarehousesRead, 10, "inventory", "Inventory", 300),
    ];

    public override IEnumerable<CustomizableEntityDefinition> CustomizableEntities =>
        [new("inventory.warehouse", "Warehouse", typeof(Warehouse), Key, WarehousesRead)];

    public override void ConfigureModel(ModelBuilder modelBuilder)
    {
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
        group.MapGet("/warehouses", async (IAppDbContext db, CancellationToken ct) =>
            await db.Set<Warehouse>().AsNoTracking().OrderBy(w => w.Code)
                .Select(w => new { w.Id, w.Code, w.Name, w.LegalEntityId, w.Address, w.IsActive }).ToListAsync(ct))
            .RequirePermission(WarehousesRead);

        group.MapPost("/warehouses", async (WarehouseInput input, IAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["Code and name are required."] });
            if (!await db.LegalEntities.AnyAsync(e => e.Id == input.LegalEntityId, ct)
                || (user.AllowedLegalEntityIds is { } allowed && !allowed.Contains(input.LegalEntityId)))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["legalEntityId"] = ["Legal entity not found."] });
            var w = new Warehouse { Code = input.Code.Trim(), Name = input.Name.Trim(), LegalEntityId = input.LegalEntityId, Address = input.Address };
            db.Set<Warehouse>().Add(w);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/m/inventory/warehouses/{w.Id}", new { w.Id, w.Code, w.Name, w.LegalEntityId });
        }).RequirePermission(WarehousesWrite);
    }
}
