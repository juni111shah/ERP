using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the schedule (critical path, milestones, baseline) of a project. Shared by reading the schedule and setting its baseline.</summary>
public sealed class ProjectScheduleBuilder(
    IProjectRepository projects,
    IProjectTaskRepository tasks,
    IProjectMilestoneRepository milestones,
    IProjectTaskDependencyRepository dependencies,
    ProjectAccess access)
{
    public async Task<ScheduleDto> BuildAsync(Guid projectId, CancellationToken ct)
    {
        var p = await projects.GetAsync(projectId, ct);
        await access.EnsureAsync(p, ct);
        var all = await tasks.ListLiveOfProjectAsync(p.Id, ct);
        var ids = all.Select(t => t.Id).ToList();
        var deps = (await dependencies.ListForTasksAsync(ids, ct)).ToLookup(d => d.TaskId, d => d.DependsOnTaskId);
        var parents = all.Where(t => t.ParentTaskId != null).Select(t => t.ParentTaskId!.Value).ToHashSet();
        var start = p.StartDate ?? all.Where(t => t.StartDate != null).Select(t => t.StartDate!.Value).DefaultIfEmpty(DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow)).Min();
        var leaves = all.Where(t => !parents.Contains(t.Id)).Select(t => new ScheduleMath.Node(t.Id, t.StartDate, t.DueDate, t.EstimatedHours, deps[t.Id].Where(ids.Contains).ToList())).ToList();
        var slots = ScheduleMath.Compute(leaves, start);
        var rows = new List<ScheduleTaskDto>();
        var today = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        bool Late(ProjectTask t, DateOnly end) => t.Status != ProjectTaskStatus.Done && (end < today || (t.DueDate is { } d && d < today));
        foreach (var t in all.OrderBy(t => t.Number))
        {
            if (slots.TryGetValue(t.Id, out var s))
                rows.Add(new ScheduleTaskDto(t.Id, t.Number, t.Title, t.ParentTaskId, t.Status, s.Start, s.End, s.Duration, s.Slack, s.Critical && deps.Any(), deps[t.Id].ToList(), t.AssignedTo, t.DueDate, Late(t, s.End), t.BaselineStart, t.BaselineEnd));
            else
            {
                var kids = all.Where(k => k.ParentTaskId == t.Id).Select(k => slots.GetValueOrDefault(k.Id)).Where(k => k != null).ToList();
                var a = kids.Count == 0 ? start : kids.Min(k => k!.Start); var b = kids.Count == 0 ? start : kids.Max(k => k!.End);
                rows.Add(new ScheduleTaskDto(t.Id, t.Number, t.Title, t.ParentTaskId, t.Status, a, b, b.DayNumber - a.DayNumber + 1, 0, false, [], t.AssignedTo, t.DueDate, Late(t, b), t.BaselineStart, t.BaselineEnd));
            }
        }
        var marks = (await milestones.ListLiveOfProjectAsync(p.Id, ct))
            .OrderBy(m => m.DueDate).Select(m => new ScheduleMilestoneDto(m.Id, m.Title, m.DueDate, m.Status)).ToList();
        var dates = rows.SelectMany(r => new[] { r.Start, r.End }).Concat(marks.Select(m => m.Date)).ToList();
        var critical = deps.Any() ? rows.Where(r => r.Critical).OrderBy(r => r.Start).Select(r => r.Id).ToList() : [];
        return new ScheduleDto(p.Id, dates.Count == 0 ? start : dates.Min(), dates.Count == 0 ? start : dates.Max(), rows, critical, marks, all.Any(t => t.BaselineStart != null));
    }
}
