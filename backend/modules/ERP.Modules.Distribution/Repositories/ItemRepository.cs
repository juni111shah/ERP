using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Repositories;

internal sealed class ItemRepository(IRepository<Item> inner) : RepositoryBase<Item>(inner), IItemRepository
{
    public Task<Dictionary<Guid, ItemSkuName>> GetSkuNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new ItemSkuName(i.Id, i.Sku, i.Name)).ToDictionaryAsync(i => i.Id, ct);

    public Task<Dictionary<Guid, Guid?>> GetTaxCodesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.TaxCodeId }).ToDictionaryAsync(i => i.Id, i => i.TaxCodeId, ct);
}
