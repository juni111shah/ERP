using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modules.Projects.Features;

/// <summary>
/// A paged list that respects the members-only setting: with "projects.restrictToMembers" on, people without the manage permission see only the rows of
/// projects they manage or belong to. Search, filters, sort and paging are the generic ones.
/// </summary>
public sealed record ScopedProjectList<T, TDto>(
    PagedQuery Paging,
    Expression<Func<T, TDto>> Projection,
    string Permission,
    Func<IReadOnlyCollection<Guid>, Expression<Func<T, bool>>> Scope,
    Func<IServiceProvider, IReadOnlyList<TDto>, CancellationToken, Task<IReadOnlyList<TDto>>>? Enrich = null,
    bool LiveOnly = false)
    : IListRequest<PagedResult<TDto>>, IQuery<PagedResult<TDto>>, IRequirePermission where T : ERP.Domain.Common.Entity
{
    public string RequiredPermission => Permission;

    public async Task<PagedResult<TDto>> ExecuteAsync(IServiceProvider services, CancellationToken ct)
    {
        var repo = services.GetRequiredService<IReadRepository<T>>();
        var ids = await services.GetRequiredService<ProjectAccess>().VisibleIdsAsync(ct);
        if (LiveOnly)
        {
            // templates and archived projects stay out of the work lists; their own project page asks with live off
            var live = await services.GetRequiredService<IReadRepository<Project>>().Query(new Specification<Project>(p => !p.IsTemplate && !p.IsArchived)).Select(p => p.Id).ToListAsync(ct);
            ids = ids is null ? live : ids.Intersect(live).ToList();
        }
        var page = await repo.PageAsync(Paging, Projection, ids is null ? null : new Specification<T>(Scope(ids)), ct);
        return Enrich is null ? page : page with { Items = await Enrich(services, page.Items, ct) };
    }
}

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
