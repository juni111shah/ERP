using ERP.Application.Abstractions;
using ERP.Domain.Catalog;

namespace ERP.Modules.Manufacturing.Repositories;

/// <summary>The catalog reads manufacturing needs. Implemented in ItemRepository.cs.</summary>
public interface IItemRepository : IRepository<Item>
{
    /// <summary>Sku, name and purchase price of those of the items that are products.</summary>
    Task<List<ProductCatalogRow>> ListProductCatalogAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Those of the items that are products, by id.</summary>
    Task<Dictionary<Guid, Item>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>The item's type; null when it does not exist.</summary>
    Task<ItemType?> GetTypeAsync(Guid id, CancellationToken ct);

    /// <summary>Sku, name and unit of the items that exist, by id.</summary>
    Task<Dictionary<Guid, ItemLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>A product's catalog row as a BOM reads it.</summary>
public sealed record ProductCatalogRow(Guid Id, string Sku, string Name, decimal? PurchasePrice);

/// <summary>An item's sku, name and unit.</summary>
public sealed record ItemLabel(Guid Id, string Sku, string Name, string Uom);
