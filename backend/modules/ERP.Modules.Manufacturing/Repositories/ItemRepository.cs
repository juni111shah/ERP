using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Repositories;

internal sealed class ItemRepository(IRepository<Item> inner) : RepositoryBase<Item>(inner), IItemRepository
{
    public Task<List<ProductCatalogRow>> ListProductCatalogAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).Select(i => new ProductCatalogRow(i.Id, i.Sku, i.Name, i.PurchasePrice)).ToListAsync(ct);

    public async Task<Dictionary<Guid, Item>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => await Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).ToDictionaryAsync(i => i.Id, ct);

    public async Task<ItemType?> GetTypeAsync(Guid id, CancellationToken ct)
        => (await Query(new Specification<Item>(i => i.Id == id)).Select(i => new { i.Type }).FirstOrDefaultAsync(ct))?.Type;

    public Task<Dictionary<Guid, ItemLabel>> GetLabelsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(i => ids.Contains(i.Id))).Select(i => new ItemLabel(i.Id, i.Sku, i.Name, i.Uom)).ToDictionaryAsync(i => i.Id, ct);
}
