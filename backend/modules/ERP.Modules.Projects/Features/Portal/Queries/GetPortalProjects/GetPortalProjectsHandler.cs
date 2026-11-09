using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetPortalProjectsHandler(
    IProjectRepository projects,
    IProjectMilestoneRepository milestones,
    IProjectTaskRepository tasks,
    ICurrentUser user)
    : IQueryHandler<GetPortalProjectsQuery, IReadOnlyList<PortalProjectDto>>
{
    public async Task<IReadOnlyList<PortalProjectDto>> Handle(GetPortalProjectsQuery query, CancellationToken ct)
    {
        // 1. The customer's projects and their milestones.
        var me = PortalCustomer.IdOf(user);
        var mine = await projects.ListForPortalCustomerAsync(me, ct);
        var ids = mine.Select(p => p.Id).ToList();
        var ms = (await milestones.ListLiveOfProjectsAsync(ids, ct)).ToLookup(m => m.ProjectId);

        // 2. How far each one is, from its tasks.
        var done = await TaskProgressAsync(ids, ct);
        return mine.OrderBy(p => p.Number).Select(p => new PortalProjectDto(p.Id, p.Number, p.Name, p.Status, p.StartDate, p.EndDate, done.GetValueOrDefault(p.Id), p.ManagerName,
            ms[p.Id].OrderBy(m => m.DueDate).Select(m => new PortalMilestoneDto(m.Id, m.Title, m.DueDate, m.Status, m.AchievedDate)).ToList())).ToList();
    }

    private async Task<Dictionary<Guid, decimal>> TaskProgressAsync(List<Guid> projectIds, CancellationToken ct)
    {
        var rows = await tasks.ListOfProjectsAsync(projectIds, ct);
        return rows.GroupBy(t => t.ProjectId).ToDictionary(g => g.Key, g => ProjectMath.PercentComplete(g));
    }
}
