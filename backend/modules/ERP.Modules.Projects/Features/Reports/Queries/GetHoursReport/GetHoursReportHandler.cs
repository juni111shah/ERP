using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetHoursReportHandler(
    IProjectRepository projects,
    IProjectTimesheetRepository timesheets,
    IProjectTaskRepository tasks,
    ProjectAccess access)
    : IQueryHandler<GetHoursReportQuery, IReadOnlyList<HoursRow>>
{
    public async Task<IReadOnlyList<HoursRow>> Handle(GetHoursReportQuery q, CancellationToken ct)
    {
        // 1. The time of the period on projects the caller may see.
        var visible = await access.VisibleIdsAsync(ct);
        var time = (await timesheets.ListNotRejectedBetweenAsync(q.From, q.To, q.ProjectId, ct)).Where(t => visible == null || visible.Contains(t.ProjectId)).ToList();

        // 2. The labels of the groups.
        Dictionary<Guid, string> names = new();
        if (q.GroupBy == "project") { var pids = time.Select(t => t.ProjectId).Distinct().ToList(); names = await projects.MapLabelsAsync(pids, ct); }
        if (q.GroupBy == "task") { var tids = time.Where(t => t.TaskId != null).Select(t => t.TaskId!.Value).Distinct().ToList(); names = await tasks.MapLabelsAsync(tids, ct); }
        string Key(ProjectTimesheet t) => q.GroupBy switch { "project" => names.GetValueOrDefault(t.ProjectId, "?"), "task" => t.TaskId is { } id ? names.GetValueOrDefault(id, "?") : "No task", _ => t.WorkerName };

        // 3. One row per group.
        return time.GroupBy(Key).OrderBy(g => g.Key)
            .Select(g => new HoursRow(g.Key, g.Sum(t => t.Hours), g.Where(t => t.IsBillable && !t.WrittenOff).Sum(t => t.Hours), g.Sum(t => t.TotalAmount), g.Sum(t => t.CostAmount))).ToList();
    }
}
