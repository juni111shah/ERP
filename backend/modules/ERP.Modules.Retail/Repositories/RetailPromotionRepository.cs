using ERP.Application.Abstractions;
using ERP.Modules.Retail.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Retail.Repositories;

internal sealed class RetailPromotionRepository(IRepository<RetailPromotion> inner) : RepositoryBase<RetailPromotion>(inner), IRetailPromotionRepository
{
    public Task<RetailPromotion?> FindByCodeAsync(string code, Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<RetailPromotion>(p =>
            p.Code == code && p.LegalEntityId == legalEntityId))
            .FirstOrDefaultAsync(ct);

    public async Task<int> TotalTimesUsedAsync(CancellationToken ct)
        => await Query().SumAsync(p => (int?)p.TimesUsed, ct) ?? 0;
}
