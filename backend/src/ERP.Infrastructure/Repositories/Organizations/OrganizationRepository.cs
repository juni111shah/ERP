using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Organizations;

internal sealed class OrganizationRepository(IRepository<Organization> organizations) : RepositoryBase<Organization>(organizations), IOrganizationRepository
{
    public Task<List<Organization>> ListClientsAsync(Guid? onlyOrg, CancellationToken ct)
        => Query().Where(o => !o.IsInternal && (onlyOrg == null || o.Id == onlyOrg)).ToListAsync(ct);

    public Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct) => Query().ToDictionaryAsync(o => o.Id, o => o.Name, ct);
}
