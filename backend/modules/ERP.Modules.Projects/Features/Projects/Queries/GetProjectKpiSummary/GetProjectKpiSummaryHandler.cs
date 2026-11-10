using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectKpiSummaryHandler(IProjectRepository projects, ProjectAccess access, ProjectFigures figures) : IQueryHandler<GetProjectKpiSummaryQuery, ProjectKpiDto>
{
    public async Task<ProjectKpiDto> Handle(GetProjectKpiSummaryQuery query, CancellationToken ct)
    {
        // templates and archived projects are left out; the figures are the ones the billing page shows
        var visible = await access.VisibleIdsAsync(ct);
        var all = (await projects.ListAllAsync(ct)).Where(x => visible == null || visible.Contains(x.Id)).ToList();
        var live = all.Where(x => !x.IsTemplate && !x.IsArchived).ToList();
        var rows = await figures.ForAsync(live, ct);
        return new ProjectKpiDto(
            live.Count,
            live.Count(x => x.Status == ProjectStatus.Active),
            live.Count(x => x.Status == ProjectStatus.Completed),
            rows.Values.Sum(x => x.Budget),
            rows.Values.Sum(x => x.LoggedHours),
            rows.Values.Sum(x => x.Unbilled),
            live.Count(x => x.Status == ProjectStatus.OnHold),
            rows.Values.Count(x => x.Health == "atRisk"),
            rows.Values.Count(x => x.Health == "offTrack"),
            rows.Values.Sum(x => x.OverdueTasks),
            rows.Values.Sum(x => x.Cost),
            all.Count(x => x.IsTemplate),
            all.Count(x => x.IsArchived && !x.IsTemplate));
    }
}
