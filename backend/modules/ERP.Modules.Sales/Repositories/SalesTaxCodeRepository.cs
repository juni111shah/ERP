using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Repositories;

internal sealed class SalesTaxCodeRepository(IRepository<TaxCode> inner) : RepositoryBase<TaxCode>(inner), ISalesTaxCodeRepository
{
    public Task<List<TaxCode>> ListActiveForLegalEntityAsync(Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<TaxCode>(t =>
            t.IsActive && (t.LegalEntityId == legalEntityId || t.LegalEntityId == null)))
            .ToListAsync(ct);
}
