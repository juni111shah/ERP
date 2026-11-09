using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Catalog;
using ERP.Modules.Inventory.Domain;
using ERP.Modules.Inventory.Repositories;

namespace ERP.Modules.Inventory.Features;

/// <summary>Checks shared by the stock commands (receive, issue, adjust, transfer): what may move, from where, and whether the stock is there.</summary>
internal sealed class StockMovementChecks(IStockMovementRepository movements, IWarehouseRepository warehouses, IStockItemRepository items, IReadRepository<ERP.Domain.Finance.Account> accounts)
{
    /// <summary>An active warehouse and a product item, or a field error.</summary>
    public async Task<(Warehouse Warehouse, Item Item)> RequireAsync(Guid warehouseId, Guid itemId, CancellationToken ct)
    {
        var wh = await warehouses.FindAsync(warehouseId, ct) ?? throw new FieldValidationException("warehouseId", "Warehouse not found.");
        if (!wh.IsActive) throw new ConflictException($"Warehouse {wh.Code} is not active.");
        var item = await items.FindAsync(itemId, ct) ?? throw new FieldValidationException("itemId", "Item not found.");
        if (item.Type != ItemType.Product) throw new FieldValidationException("itemId", "Only product items are kept in stock.");
        return (wh, item);
    }

    public async Task RequireAccountAsync(Guid accountId, string field, CancellationToken ct)
    {
        if (!await accounts.AnyAsync(a => a.Id == accountId && a.IsActive && a.IsPostable, ct)) throw new FieldValidationException(field, "Choose an active, postable account.");
    }

    /// <summary>Position of one item in one warehouse, optionally only up to a date (so a backdated issue cannot use stock that arrives later).</summary>
    public async Task<StockPosition> PositionAsync(Guid warehouseId, Guid itemId, CancellationToken ct, DateOnly? asOf = null)
        => StockCosting.Replay(await movements.ListForItemInWarehouseUpToAsync(warehouseId, itemId, asOf, ct));

    /// <summary>A backdated movement must not use stock that later movements already consumed.</summary>
    public async Task EnsureNoShortfallAsync(StockMovement candidate, CancellationToken ct)
    {
        var existing = await movements.ListForItemInWarehouseAsync(candidate.WarehouseId, candidate.ItemId, ct);
        if (StockCosting.FirstShortfall(existing, candidate) is { } day)
            throw new ConflictException($"This would leave stock negative on {day:yyyy-MM-dd}; later movements already use that stock.");
    }

    /// <summary>A new movement of the warehouse's legal entity.</summary>
    public static StockMovement New(Warehouse wh, Guid itemId, DateOnly date, MovementKind kind, decimal qty, decimal cost, string? reference, string? memo)
        => new() { LegalEntityId = wh.LegalEntityId, WarehouseId = wh.Id, ItemId = itemId, Date = date, Kind = kind, Quantity = qty, UnitCost = cost, Reference = reference, Memo = memo };
}
