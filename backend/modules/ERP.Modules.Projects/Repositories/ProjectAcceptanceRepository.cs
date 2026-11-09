using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectAcceptanceRepository(IRepository<ProjectAcceptance> inner) : RepositoryBase<ProjectAcceptance>(inner), IProjectAcceptanceRepository
{
    public Task<List<ProjectAcceptance>> ListByProjectAsync(Guid? projectId, CancellationToken ct)
        => Query(new Specification<ProjectAcceptance>(a => projectId == null || a.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectAcceptance>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectAcceptance>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectAcceptance>> ListOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectAcceptance>(a => projectIds.Contains(a.ProjectId))).ToListAsync(ct);
}
