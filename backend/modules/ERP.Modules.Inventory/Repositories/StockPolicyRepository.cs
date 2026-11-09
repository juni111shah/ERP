using ERP.Application.Abstractions;
using ERP.Modules.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Inventory.Repositories;

internal sealed class StockPolicyRepository(IRepository<StockPolicy> inner) : RepositoryBase<StockPolicy>(inner), IStockPolicyRepository
{
    public Task<StockPolicy?> FindForItemForUpdateAsync(Guid itemId, CancellationToken ct)
        => Tracked(new Specification<StockPolicy>(p => p.ItemId == itemId)).FirstOrDefaultAsync(ct);
}
