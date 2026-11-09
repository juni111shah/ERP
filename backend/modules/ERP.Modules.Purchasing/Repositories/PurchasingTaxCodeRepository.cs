using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchasingTaxCodeRepository(IRepository<TaxCode> inner) : RepositoryBase<TaxCode>(inner), IPurchasingTaxCodeRepository
{
    public Task<TaxCode?> FindBestForRateAsync(decimal rate, Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<TaxCode>(t => t.Rate == rate && t.IsActive && (t.LegalEntityId == null || t.LegalEntityId == legalEntityId)))
            .OrderByDescending(t => t.IsRecoverable).ThenByDescending(t => t.IsDefault).ThenBy(t => t.Code)
            .FirstOrDefaultAsync(ct);
}
