using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectRateRepository(IRepository<ProjectRate> inner) : RepositoryBase<ProjectRate>(inner), IProjectRateRepository
{
    public Task<List<ProjectRate>> ListForProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectRate>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectRate>> ListApplicableAsync(Guid? projectId, CancellationToken ct)
        => Query(new Specification<ProjectRate>(r => projectId == null || r.ProjectId == projectId || r.ProjectId == null)).ToListAsync(ct);

    public Task<List<ProjectRate>> ListForProjectAndUserAsync(Guid? projectId, Guid? userId, CancellationToken ct)
        => Query(new Specification<ProjectRate>(r => r.ProjectId == projectId && r.UserId == userId)).ToListAsync(ct);
}
