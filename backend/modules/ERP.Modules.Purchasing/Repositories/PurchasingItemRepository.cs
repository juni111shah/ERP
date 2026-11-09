using ERP.Application.Abstractions;
using ERP.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchasingItemRepository(IRepository<Item> inner) : RepositoryBase<Item>(inner), IPurchasingItemRepository
{
    public Task<List<Guid>> ListProductIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Item>(x => ids.Contains(x.Id) && x.Type == ItemType.Product))
            .Select(x => x.Id)
            .ToListAsync(ct);
}
