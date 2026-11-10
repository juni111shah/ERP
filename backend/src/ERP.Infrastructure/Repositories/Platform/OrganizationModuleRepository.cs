using ERP.Application.Abstractions;
using ERP.Domain.Organizations;
using ERP.Modularity.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class OrganizationModuleRepository(IRepository<OrganizationModule> modules) : RepositoryBase<OrganizationModule>(modules), IOrganizationModuleRepository
{
    public Task<OrganizationModule?> FindForUpdateAsync(string moduleKey, CancellationToken ct)
        => Tracked(new Specification<OrganizationModule>(x => x.ModuleKey == moduleKey)).FirstOrDefaultAsync(ct);
}
