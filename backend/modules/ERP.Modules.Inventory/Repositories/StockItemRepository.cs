using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Repositories;

internal sealed class StockItemRepository(IRepository<Item> inner) : RepositoryBase<Item>(inner), IStockItemRepository
{
    public Task<Dictionary<Guid, Item>> MapByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).ToDictionaryAsync(i => i.Id, ct);

    public Task<Dictionary<Guid, StockItemInfo>> MapInfoAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new StockItemInfo(i.Id, i.Sku, i.Name, i.Uom)).ToDictionaryAsync(i => i.Id, ct);

    public Task<Dictionary<Guid, StockItemName>> MapNamesAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new StockItemName(i.Id, i.Sku, i.Name)).ToDictionaryAsync(i => i.Id, ct);
}
