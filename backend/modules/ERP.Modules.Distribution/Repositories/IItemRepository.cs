using ERP.Application.Abstractions;
using ERP.Domain.Catalog;

namespace ERP.Modules.Distribution.Repositories;

/// <summary>The catalog reads distribution needs. Implemented in ItemRepository.cs.</summary>
public interface IItemRepository : IRepository<Item>
{
    /// <summary>Sku and name of the items that exist, by id.</summary>
    Task<Dictionary<Guid, ItemSkuName>> GetSkuNamesAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The tax code of each of the items that exist, by id.</summary>
    Task<Dictionary<Guid, Guid?>> GetTaxCodesAsync(List<Guid> ids, CancellationToken ct);
}

/// <summary>An item's sku and name.</summary>
public sealed record ItemSkuName(Guid Id, string Sku, string Name);
