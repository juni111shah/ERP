using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Organizations;

internal sealed class LegalEntityRepository(IRepository<LegalEntity> legalEntities) : RepositoryBase<LegalEntity>(legalEntities), ILegalEntityRepository
{
    public Task<List<string>> ListBaseCurrenciesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query().Where(e => legalEntityId == null || e.Id == legalEntityId).Select(e => e.BaseCurrency).Distinct().ToListAsync(ct);
}
