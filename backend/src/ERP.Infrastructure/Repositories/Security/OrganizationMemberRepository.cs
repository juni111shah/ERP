using ERP.Application.Abstractions;
using ERP.Application.Security;
using ERP.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Security;

internal sealed class OrganizationMemberRepository(IRepository<OrganizationMember> members) : RepositoryBase<OrganizationMember>(members), IOrganizationMemberRepository
{
    public Task<OrganizationMember?> FindByUserForUpdateAsync(Guid userId, CancellationToken ct)
        => Tracked(new Specification<OrganizationMember>(m => m.UserId == userId)).FirstOrDefaultAsync(ct);
}
