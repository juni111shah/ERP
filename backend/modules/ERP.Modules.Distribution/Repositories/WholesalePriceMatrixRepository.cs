using ERP.Application.Abstractions;
using ERP.Modules.Distribution.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Distribution.Repositories;

internal sealed class WholesalePriceMatrixRepository(IRepository<WholesalePriceMatrix> inner) : RepositoryBase<WholesalePriceMatrix>(inner), IWholesalePriceMatrixRepository
{
    public Task<WholesalePriceMatrix?> FindMatchingRuleAsync(Guid legalEntityId, Guid itemId, WholesaleTier tier, decimal quantity, CancellationToken ct)
        => Query(new Specification<WholesalePriceMatrix>(p =>
            p.LegalEntityId == legalEntityId &&
            p.ItemId == itemId &&
            p.Tier == tier &&
            p.IsActive &&
            p.MinQuantity <= quantity &&
            (!p.MaxQuantity.HasValue || p.MaxQuantity.Value >= quantity)))
            .OrderByDescending(p => p.MinQuantity)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountActiveAsync(CancellationToken ct)
        => Query(new Specification<WholesalePriceMatrix>(w => w.IsActive)).CountAsync(ct);
}
