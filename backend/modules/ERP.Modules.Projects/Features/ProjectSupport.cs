using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>With the setting "projects.restrictToMembers" on, people without the manage permission only reach projects they manage or belong to.</summary>
public sealed class ProjectAccess(ICurrentUser user, ISettingsService settings, IReadRepository<ProjectMember> members)
{
    public async Task EnsureAsync(Project p, CancellationToken ct)
    {
        if (user.PortalKind != null) return; // the customer portal has its own endpoints and checks
        if (await settings.GetAsync<bool?>("projects.restrictToMembers", p.LegalEntityId, ct) != true) return;
        if (user.Permissions.Contains(P.ProjectsManage) || user.Permissions.Contains("*")) return;
        if (user.UserId is not { } me) throw new ForbiddenException();
        if (p.ManagerUserId == me) return;
        if (await members.AnyAsync(m => m.ProjectId == p.Id && m.UserId == me, ct)) return;
        throw new ForbiddenException();
    }
}

/// <summary>Display names for sign-in accounts, so a task or an hour of work can point at a real person.</summary>
public sealed class ProjectPeople(IUserDirectory directory)
{
    public async Task<string?> NameAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is not { } id) return null;
        var profile = await directory.GetProfileAsync(id, ct);
        return profile?.FullName;
    }

    /// <summary>The name of a person who must be a member of the workspace; throws a field error when not.</summary>
    public async Task<string> RequireNameAsync(Guid userId, string field, CancellationToken ct)
    {
        var members = await directory.ListMembersAsync([userId], ct);
        return members.FirstOrDefault()?.FullName ?? throw new FieldValidationException(field, "That person is not a member of this workspace.");
    }

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, string>();
        return (await directory.ListMembersAsync(list, ct)).ToDictionary(m => m.Id, m => m.FullName);
    }
}

/// <summary>Finds the bill rate and the cost rate of an hour: person on this project, person, role on this project, project, any (workspace-wide), then the workspace default.</summary>
public sealed class RateResolver(IReadRepository<ProjectRate> rates, ISettingsService settings)
{
    public async Task<(decimal Bill, decimal Cost)> ResolveAsync(Guid projectId, Guid? userId, string? role, Guid legalEntityId, CancellationToken ct)
    {
        var rows = await rates.Query(new Specification<ProjectRate>(r => r.ProjectId == projectId || r.ProjectId == null)).ToListAsync(ct);
        var hit = Pick(rows, projectId, userId, role);
        if (hit is not null) return (hit.BillRate, hit.CostRate);
        var fallback = await settings.GetAsync<decimal?>("projects.defaultHourlyRate", legalEntityId, ct) ?? 0m;
        return (fallback, 0m);
    }

    public static ProjectRate? Pick(IReadOnlyCollection<ProjectRate> rows, Guid projectId, Guid? userId, string? role)
    {
        bool Role(ProjectRate r) => !string.IsNullOrWhiteSpace(role) && string.Equals(r.Role, role, StringComparison.OrdinalIgnoreCase);
        return rows.FirstOrDefault(r => r.ProjectId == projectId && userId != null && r.UserId == userId)
            ?? rows.FirstOrDefault(r => r.ProjectId == null && userId != null && r.UserId == userId)
            ?? rows.FirstOrDefault(r => r.ProjectId == projectId && r.UserId == null && Role(r))
            ?? rows.FirstOrDefault(r => r.ProjectId == null && r.UserId == null && Role(r))
            ?? rows.FirstOrDefault(r => r.ProjectId == projectId && r.UserId == null && string.IsNullOrWhiteSpace(r.Role))
            ?? rows.FirstOrDefault(r => r.ProjectId == null && r.UserId == null && string.IsNullOrWhiteSpace(r.Role));
    }
}

/// <summary>Budget arithmetic shared by the project screens and the reports; no I/O.</summary>
public static class ProjectMath
{
    public static decimal EffectiveBudget(decimal budget, IEnumerable<ProjectChangeOrder> changeOrders) =>
        budget + changeOrders.Where(c => c.Status == ChangeOrderStatus.Approved).Sum(c => c.AmountChange);

    public static decimal EffectiveBudgetHours(decimal hours, IEnumerable<ProjectChangeOrder> changeOrders) =>
        hours + changeOrders.Where(c => c.Status == ChangeOrderStatus.Approved).Sum(c => c.HoursChange);

    /// <summary>"onTrack", "atRisk" or "offTrack" from spend against budget, a past end date with open work, and overdue tasks.</summary>
    public static string Health(Project p, decimal budget, decimal cost, int overdueTasks, int openTasks, DateOnly today)
    {
        if (p.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) return "closed";
        var used = budget > 0 ? cost / budget : 0m;
        var late = p.EndDate is { } end && end < today && openTasks > 0;
        if (used > 1m || (late && overdueTasks > 0 && used > 0.9m)) return "offTrack";
        if (used > 0.85m || late || overdueTasks > 0) return "atRisk";
        return "onTrack";
    }

    /// <summary>Share of the planned effort that is finished, 0 to 100; tasks with no estimate count as one hour so they still move the bar.</summary>
    public static decimal PercentComplete(IEnumerable<ProjectTask> tasks)
    {
        var live = tasks.Where(t => t.Status != ProjectTaskStatus.Cancelled).ToList();
        if (live.Count == 0) return 0m;
        decimal Weight(ProjectTask t) => t.EstimatedHours > 0 ? t.EstimatedHours : 1m;
        var total = live.Sum(Weight);
        return total == 0 ? 0m : Math.Round(live.Where(t => t.Status == ProjectTaskStatus.Done).Sum(Weight) / total * 100m, 1);
    }

    public static decimal Round(decimal value) => ERP.Application.Finance.Money.Round(value);
}

/// <summary>Keeps the "actual hours" of a task equal to the time logged against it.</summary>
public static class TaskHours
{
    public static async Task RecalculateAsync(IRepository<ProjectTimesheet> timesheets, IRepository<ProjectTask> tasks, Guid? taskId, CancellationToken ct)
    {
        if (taskId is not { } id) return;
        var task = await tasks.Tracked(new Specification<ProjectTask>(t => t.Id == id)).FirstOrDefaultAsync(ct);
        if (task is null) return;
        task.ActualHours = await timesheets.Query(new Specification<ProjectTimesheet>(t => t.TaskId == id)).SumDecimalAsync(t => t.Hours, ct);
    }
}

public static class QueryExtensions
{
    /// <summary>Adds up a decimal column on the client: SQLite (used by the tests) cannot sum decimals in the database, and the rows are few.</summary>
    public static async Task<decimal> SumDecimalAsync<T>(this IQueryable<T> query, System.Linq.Expressions.Expression<Func<T, decimal>> selector, CancellationToken ct)
        => (await query.Select(selector).ToListAsync(ct)).Sum();
}
