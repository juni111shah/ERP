using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Organizations;

internal sealed class LegalEntityRepository(IRepository<LegalEntity> legalEntities) : RepositoryBase<LegalEntity>(legalEntities), ILegalEntityRepository
{
    public Task<List<LegalEntity>> ListAllowedAsync(IReadOnlyList<Guid>? allowed, CancellationToken ct)
        => Query().Where(e => allowed == null || allowed.Contains(e.Id)).OrderBy(e => e.Code).ToListAsync(ct);

    public Task<List<Guid>> ListExistingIdsAsync(Guid[] ids, CancellationToken ct)
        => Query(new Specification<LegalEntity>(e => ids.Contains(e.Id))).Select(e => e.Id).ToListAsync(ct);
}
