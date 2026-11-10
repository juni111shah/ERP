using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectRiskRepository(IRepository<ProjectRisk> inner) : RepositoryBase<ProjectRisk>(inner), IProjectRiskRepository
{
    public Task<List<ProjectRisk>> ListByProjectAsync(Guid? projectId, CancellationToken ct)
        => Query(new Specification<ProjectRisk>(r => projectId == null || r.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectRisk>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectRisk>(x => x.ProjectId == projectId)).ToListAsync(ct);
}
