using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectBudgetLineRepository(IRepository<ProjectBudgetLine> inner) : RepositoryBase<ProjectBudgetLine>(inner), IProjectBudgetLineRepository
{
    public Task<List<ProjectBudgetLine>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectBudgetLine>(x => x.ProjectId == projectId)).ToListAsync(ct);
}
