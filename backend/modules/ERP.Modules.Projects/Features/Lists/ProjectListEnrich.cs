using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Projects.Features;

/// <summary>Fills in what a list projection cannot reach: names from other tables, computed figures, and hiding cost from people who may not see it.</summary>
internal static class ProjectListEnrich
{
    private static async Task<Dictionary<Guid, string>> ProjectNamesAsync(IServiceProvider s, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0) return [];
        return await s.GetRequiredService<IReadRepository<Project>>().Query(new Specification<Project>(p => wanted.Contains(p.Id))).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
    }

    public static async Task<IReadOnlyList<ProjectSummaryDto>> Projects(IServiceProvider s, IReadOnlyList<ProjectSummaryDto> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var entities = await s.GetRequiredService<IReadRepository<Project>>().Query(new Specification<Project>(p => ids.Contains(p.Id))).ToListAsync(ct);
        var figures = await s.GetRequiredService<ProjectFigures>().ForAsync(entities, ct);
        return rows.Select(r => figures.TryGetValue(r.Id, out var f)
            ? r with { PercentComplete = f.PercentComplete, Health = f.Health, Cost = f.Cost, EffectiveBudget = f.Budget, OpenTasks = f.OpenTasks, OverdueTasks = f.OverdueTasks, UnbilledAmount = f.Unbilled }
            : r).ToList();
    }

    public static async Task<IReadOnlyList<TaskDto>> Tasks(IServiceProvider s, IReadOnlyList<TaskDto> rows, CancellationToken ct)
    {
        var names = await ProjectNamesAsync(s, rows.Select(r => r.ProjectId), ct);
        var ms = rows.Where(r => r.MilestoneId != null).Select(r => r.MilestoneId!.Value).Distinct().ToList();
        var titles = ms.Count == 0 ? new Dictionary<Guid, string>() : await s.GetRequiredService<IReadRepository<ProjectMilestone>>().Query(new Specification<ProjectMilestone>(m => ms.Contains(m.Id))).ToDictionaryAsync(m => m.Id, m => m.Title, ct);
        return rows.Select(r => r with { ProjectName = names.GetValueOrDefault(r.ProjectId, ""), MilestoneTitle = r.MilestoneId is { } id ? titles.GetValueOrDefault(id) : null }).ToList();
    }

    public static async Task<IReadOnlyList<TimesheetDto>> Timesheets(IServiceProvider s, IReadOnlyList<TimesheetDto> rows, CancellationToken ct)
    {
        var names = await ProjectNamesAsync(s, rows.Select(r => r.ProjectId), ct);
        var ts = rows.Where(r => r.TaskId != null).Select(r => r.TaskId!.Value).Distinct().ToList();
        var titles = ts.Count == 0 ? new Dictionary<Guid, string>() : await s.GetRequiredService<IReadRepository<ProjectTask>>().Query(new Specification<ProjectTask>(t => ts.Contains(t.Id))).ToDictionaryAsync(t => t.Id, t => t.Title, ct);
        var cost = s.GetRequiredService<ProjectAccess>().CanSeeCost;
        return rows.Select(r => r with
        {
            ProjectName = names.GetValueOrDefault(r.ProjectId, ""), TaskTitle = r.TaskId is { } id ? titles.GetValueOrDefault(id) : null,
            CostRate = cost ? r.CostRate : 0m, CostAmount = cost ? r.CostAmount : 0m,
        }).ToList();
    }

    public static async Task<IReadOnlyList<ExpenseDto>> Expenses(IServiceProvider s, IReadOnlyList<ExpenseDto> rows, CancellationToken ct)
    {
        var names = await ProjectNamesAsync(s, rows.Select(r => r.ProjectId), ct);
        return rows.Select(r => r with { ProjectName = names.GetValueOrDefault(r.ProjectId, "") }).ToList();
    }

    public static async Task<IReadOnlyList<MilestoneDto>> Milestones(IServiceProvider s, IReadOnlyList<MilestoneDto> rows, CancellationToken ct)
    {
        var names = await ProjectNamesAsync(s, rows.Select(r => r.ProjectId), ct);
        return rows.Select(r => r with { ProjectName = names.GetValueOrDefault(r.ProjectId, "") }).ToList();
    }
}
