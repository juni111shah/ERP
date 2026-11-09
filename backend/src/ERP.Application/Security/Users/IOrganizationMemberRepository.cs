using ERP.Application.Abstractions;
using ERP.Domain.Identity;

namespace ERP.Application.Security;

/// <summary>Data access for workspace memberships. Implemented in ERP.Infrastructure/Repositories/Security/OrganizationMemberRepository.cs.</summary>
public interface IOrganizationMemberRepository : IRepository<OrganizationMember>
{
    /// <summary>The membership of <paramref name="userId"/> in this workspace, tracked for changes, or null.</summary>
    Task<OrganizationMember?> FindByUserForUpdateAsync(Guid userId, CancellationToken ct);
}
