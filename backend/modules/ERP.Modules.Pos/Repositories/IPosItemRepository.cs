using ERP.Application.Abstractions;
using ERP.Domain.Catalog;

namespace ERP.Modules.Pos.Repositories;

/// <summary>A page of the till's catalogue and how many items match in all.</summary>
public sealed record PosCatalogPage(List<Item> Items, int Total);

/// <summary>The items as the till reads them (catalogue look-ups and pricing). Implemented in PosItemRepository.cs.</summary>
public interface IPosItemRepository : IRepository<Item>
{
    /// <summary>The items with these ids, by id.</summary>
    Task<Dictionary<Guid, Item>> MapByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>
    /// A page of the active items by name: <paramref name="code"/> is an exact look-up (barcode or SKU); otherwise
    /// <paramref name="search"/> matches part of the name, SKU or barcode.
    /// </summary>
    Task<PosCatalogPage> PageActiveAsync(string? code, string? search, int page, int pageSize, CancellationToken ct);
}
