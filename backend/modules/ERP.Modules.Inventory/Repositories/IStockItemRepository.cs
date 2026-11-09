using ERP.Application.Abstractions;
using ERP.Domain.Catalog;

namespace ERP.Modules.Inventory.Repositories;

/// <summary>Data access for the catalog items that are kept in stock.</summary>
public interface IStockItemRepository : IRepository<Item>
{
    /// <summary>The items with the given ids, by id.</summary>
    Task<Dictionary<Guid, Item>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>SKU, name and unit of each of the items that exists, by id.</summary>
    Task<Dictionary<Guid, StockItemInfo>> MapInfoAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>SKU and name of each of the items that exists, by id.</summary>
    Task<Dictionary<Guid, StockItemName>> MapNamesAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>What the stock screens show of an item.</summary>
public sealed record StockItemInfo(Guid Id, string Sku, string Name, string Uom);

/// <summary>SKU and name of an item.</summary>
public sealed record StockItemName(Guid Id, string Sku, string Name);
