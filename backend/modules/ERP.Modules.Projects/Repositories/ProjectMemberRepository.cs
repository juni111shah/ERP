using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectMemberRepository(IRepository<ProjectMember> inner) : RepositoryBase<ProjectMember>(inner), IProjectMemberRepository
{
    public Task<string?> GetRoleAsync(Guid projectId, Guid userId, CancellationToken ct)
        => Query(new Specification<ProjectMember>(m => m.ProjectId == projectId && m.UserId == userId)).Select(m => m.Role).FirstOrDefaultAsync(ct);

    public Task<List<ProjectMember>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMember>(m => m.ProjectId == projectId)).ToListAsync(ct);
}
