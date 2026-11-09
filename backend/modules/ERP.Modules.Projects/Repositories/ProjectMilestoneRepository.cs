using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Repositories;

internal sealed class ProjectMilestoneRepository(IRepository<ProjectMilestone> inner) : RepositoryBase<ProjectMilestone>(inner), IProjectMilestoneRepository
{
    public Task<int> CountAchievedAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => m.ProjectId == projectId && m.Status == MilestoneStatus.Achieved)).CountAsync(ct);

    public Task<string?> GetTitleAsync(Guid id, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.Id == id)).Select(x => x.Title).FirstOrDefaultAsync(ct);

    public Task<decimal> SumLiveAmountAsync(Guid projectId, Guid? exceptId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.ProjectId == projectId && x.Status != MilestoneStatus.Cancelled && (exceptId == null || x.Id != exceptId))).SumDecimalAsync(x => x.Amount, ct);

    public Task<decimal> SumInvoicedAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.ProjectId == projectId && x.Status == MilestoneStatus.Invoiced)).SumDecimalAsync(x => x.Amount, ct);

    public Task<List<ProjectMilestone>> ListAchievedAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.ProjectId == projectId && x.Status == MilestoneStatus.Achieved)).OrderBy(x => x.DueDate).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListChosenAchievedForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct)
    {
        var msQuery = Tracked(new Specification<ProjectMilestone>(x => x.ProjectId == projectId && x.Status == MilestoneStatus.Achieved));
        if (onlyIds is not null) { var ids = onlyIds.ToHashSet(); msQuery = msQuery.Where(x => ids.Contains(x.Id)); }
        return msQuery.ToListAsync(ct);
    }

    public Task<List<ProjectAmount>> ListAchievedAmountsAsync(CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Achieved)).Select(x => new ProjectAmount(x.ProjectId, x.Amount)).ToListAsync(ct);

    public Task<List<ProjectAmount>> ListInvoicedAmountsAsync(CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Invoiced)).Select(x => new ProjectAmount(x.ProjectId, x.Amount)).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(x => x.ProjectId == projectId)).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListLiveOfProjectAsync(Guid projectId, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => m.ProjectId == projectId && m.Status != MilestoneStatus.Cancelled)).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListLiveOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => projectIds.Contains(m.ProjectId) && m.Status != MilestoneStatus.Cancelled)).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListAchievedOrInvoicedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => projectIds.Contains(m.ProjectId) && (m.Status == MilestoneStatus.Achieved || m.Status == MilestoneStatus.Invoiced))).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListInvoicedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => projectIds.Contains(m.ProjectId) && m.Status == MilestoneStatus.Invoiced)).ToListAsync(ct);

    public Task<List<ProjectMilestone>> ListAchievedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct)
        => Query(new Specification<ProjectMilestone>(m => projectIds.Contains(m.ProjectId) && m.Status == MilestoneStatus.Achieved)).ToListAsync(ct);
}
