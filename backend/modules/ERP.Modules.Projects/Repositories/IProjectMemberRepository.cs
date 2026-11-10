using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for project team members. Implemented in ProjectMemberRepository.cs.</summary>
public interface IProjectMemberRepository : IRepository<ProjectMember>
{
    /// <summary>The role of a person on the project; null when they are not on the team or have no role.</summary>
    Task<string?> GetRoleAsync(Guid projectId, Guid userId, CancellationToken ct);

    /// <summary>The team of the project, read-only.</summary>
    Task<List<ProjectMember>> ListForProjectAsync(Guid projectId, CancellationToken ct);
}
