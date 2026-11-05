using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed record ScheduleTaskDto(Guid Id, string Number, string Title, Guid? ParentTaskId, ProjectTaskStatus Status, DateOnly Start, DateOnly End, int DurationDays, int SlackDays, bool Critical, IReadOnlyList<Guid> DependsOn, string? AssignedTo);
public sealed record ScheduleDto(Guid ProjectId, DateOnly Start, DateOnly Finish, IReadOnlyList<ScheduleTaskDto> Tasks, IReadOnlyList<Guid> CriticalPath);

public sealed record GetProjectSchedule(Guid ProjectId) : IQuery<ScheduleDto>, IRequirePermission { public string RequiredPermission => P.TasksRead; }

public sealed record CloneInput(string Name, DateOnly? StartDate, Guid? CustomerId, bool IncludeTeam = false, bool AsTemplate = false);
public sealed record CloneProject(Guid Id, CloneInput Input) : ICommand<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record ArchiveProject(Guid Id, bool Archive) : ICommand<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }

public sealed class CloneProjectValidator : AbstractValidator<CloneProject>
{
    public CloneProjectValidator() { RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(200); }
}

/// <summary>Critical-path scheduling over task durations and dependencies; no I/O. Days are calendar days.</summary>
public static class ScheduleMath
{
    public sealed record Node(Guid Id, DateOnly? Start, DateOnly? Due, decimal EstimatedHours, IReadOnlyList<Guid> DependsOn);
    public sealed record Slot(Guid Id, DateOnly Start, DateOnly End, int Duration, int Slack, bool Critical);

    public static int DurationOf(Node n)
    {
        if (n.Start is { } s && n.Due is { } d && d >= s) return d.DayNumber - s.DayNumber + 1;
        return n.EstimatedHours > 0 ? Math.Max(1, (int)Math.Ceiling(n.EstimatedHours / 8m)) : 1;
    }

    public static IReadOnlyDictionary<Guid, Slot> Compute(IReadOnlyList<Node> nodes, DateOnly projectStart)
    {
        var byId = nodes.ToDictionary(n => n.Id);
        var dur = nodes.ToDictionary(n => n.Id, DurationOf);
        var es = new Dictionary<Guid, int>(); // day number of the earliest start
        int Es(Guid id, HashSet<Guid> path)
        {
            if (es.TryGetValue(id, out var v)) return v;
            if (!path.Add(id)) return projectStart.DayNumber; // a loop cannot be scheduled; the service refuses to create one, this is only a guard
            var n = byId[id];
            var start = (n.Start ?? projectStart).DayNumber;
            foreach (var d in n.DependsOn.Where(byId.ContainsKey)) start = Math.Max(start, Es(d, path) + dur[d]);
            path.Remove(id);
            return es[id] = start;
        }
        foreach (var n in nodes) Es(n.Id, []);
        var finish = nodes.Count == 0 ? projectStart.DayNumber : nodes.Max(n => es[n.Id] + dur[n.Id] - 1);

        var successors = nodes.SelectMany(n => n.DependsOn.Where(byId.ContainsKey).Select(d => (From: d, To: n.Id))).ToLookup(e => e.From, e => e.To);
        var lf = new Dictionary<Guid, int>();
        int Lf(Guid id)
        {
            if (lf.TryGetValue(id, out var v)) return v;
            var next = successors[id].ToList();
            var late = next.Count == 0 ? finish : next.Min(s => Lf(s) - dur[s]);
            return lf[id] = late;
        }
        var result = new Dictionary<Guid, Slot>();
        foreach (var n in nodes)
        {
            var slack = Lf(n.Id) - (es[n.Id] + dur[n.Id] - 1);
            result[n.Id] = new Slot(n.Id, DateOnly.FromDayNumber(es[n.Id]), DateOnly.FromDayNumber(es[n.Id] + dur[n.Id] - 1), dur[n.Id], slack, slack == 0);
        }
        return result;
    }
}

internal sealed class PlanningHandlers(
    IRepository<Project> projects,
    IRepository<ProjectTask> tasks,
    IRepository<ProjectMilestone> milestones,
    IRepository<ProjectTaskDependency> dependencies,
    IRepository<ProjectMember> members,
    IRepository<ProjectRate> rates,
    IReadRepository<ProjectChangeOrder> changeOrders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ISender sender) :
    IRequestHandler<GetProjectSchedule, ScheduleDto>,
    IRequestHandler<CloneProject, ProjectDto>,
    IRequestHandler<ArchiveProject, ProjectDto>
{
    public async Task<ScheduleDto> Handle(GetProjectSchedule q, CancellationToken ct)
    {
        var p = await projects.GetAsync(q.ProjectId, ct);
        var all = await tasks.Query(new Specification<ProjectTask>(t => t.ProjectId == p.Id && t.Status != ProjectTaskStatus.Cancelled)).ToListAsync(ct);
        var ids = all.Select(t => t.Id).ToList();
        var deps = (await dependencies.Query(new Specification<ProjectTaskDependency>(d => ids.Contains(d.TaskId))).ToListAsync(ct)).ToLookup(d => d.TaskId, d => d.DependsOnTaskId);
        var parents = all.Where(t => t.ParentTaskId != null).Select(t => t.ParentTaskId!.Value).ToHashSet();
        var start = p.StartDate ?? all.Where(t => t.StartDate != null).Select(t => t.StartDate!.Value).DefaultIfEmpty(DateOnly.FromDateTime(DateTime.UtcNow)).Min();
        var leaves = all.Where(t => !parents.Contains(t.Id)).Select(t => new ScheduleMath.Node(t.Id, t.StartDate, t.DueDate, t.EstimatedHours, deps[t.Id].Where(ids.Contains).ToList())).ToList();
        var slots = ScheduleMath.Compute(leaves, start);
        var rows = new List<ScheduleTaskDto>();
        foreach (var t in all.OrderBy(t => t.Number))
        {
            if (slots.TryGetValue(t.Id, out var s))
                rows.Add(new ScheduleTaskDto(t.Id, t.Number, t.Title, t.ParentTaskId, t.Status, s.Start, s.End, s.Duration, s.Slack, s.Critical && deps.Any(), deps[t.Id].ToList(), t.AssignedTo));
            else
            {
                var kids = all.Where(k => k.ParentTaskId == t.Id).Select(k => slots.GetValueOrDefault(k.Id)).Where(k => k != null).ToList();
                var a = kids.Count == 0 ? start : kids.Min(k => k!.Start); var b = kids.Count == 0 ? start : kids.Max(k => k!.End);
                rows.Add(new ScheduleTaskDto(t.Id, t.Number, t.Title, t.ParentTaskId, t.Status, a, b, b.DayNumber - a.DayNumber + 1, 0, false, [], t.AssignedTo));
            }
        }
        var finish = rows.Count == 0 ? start : rows.Max(r => r.End);
        var critical = deps.Any() ? rows.Where(r => r.Critical).OrderBy(r => r.Start).Select(r => r.Id).ToList() : [];
        return new ScheduleDto(p.Id, rows.Count == 0 ? start : rows.Min(r => r.Start), finish, rows, critical);
    }

    public async Task<ProjectDto> Handle(CloneProject cmd, CancellationToken ct)
    {
        var src = await projects.GetAsync(cmd.Id, ct);
        var i = cmd.Input;
        var entity = await guard.RequireAsync(src.LegalEntityId, ct);
        var newStart = i.StartDate ?? src.StartDate;
        var shift = src.StartDate is { } s0 && newStart is { } n0 ? n0.DayNumber - s0.DayNumber : 0;
        DateOnly? Move(DateOnly? d) => d is { } v ? DateOnly.FromDayNumber(v.DayNumber + shift) : null;

        var copy = new Project
        {
            LegalEntityId = entity.Id, Name = i.Name.Trim(), Description = src.Description, CustomerId = i.AsTemplate ? null : i.CustomerId ?? (src.IsTemplate ? null : src.CustomerId),
            ManagerUserId = src.ManagerUserId, ManagerName = src.ManagerName, Status = ProjectStatus.Planning, BillingType = src.BillingType, BudgetAmount = src.BudgetAmount, BudgetHours = src.BudgetHours,
            TaxCodeId = src.TaxCodeId, StartDate = newStart, EndDate = Move(src.EndDate), IsTemplate = i.AsTemplate,
        };
        if (copy.CustomerId is { } cid) copy.CustomerName = (await sender.Send(new LookupCustomerName(cid), ct));
        copy.Number = await numbers.NextAsync(Docs.Project, entity.Id, null, ct);
        projects.Add(copy);
        await projects.SaveChangesAsync(ct);

        var srcMilestones = await milestones.Query(new Specification<ProjectMilestone>(m => m.ProjectId == src.Id && m.Status != MilestoneStatus.Cancelled)).ToListAsync(ct);
        var msMap = new Dictionary<Guid, Guid>();
        foreach (var m in srcMilestones)
        {
            var nm = new ProjectMilestone { ProjectId = copy.Id, Title = m.Title, DueDate = Move(m.DueDate) ?? m.DueDate, Amount = m.Amount, Status = MilestoneStatus.Pending };
            milestones.Add(nm); msMap[m.Id] = nm.Id;
        }
        var srcTasks = await tasks.Query(new Specification<ProjectTask>(t => t.ProjectId == src.Id && t.Status != ProjectTaskStatus.Cancelled)).ToListAsync(ct);
        var taskMap = new Dictionary<Guid, Guid>();
        foreach (var t in srcTasks.OrderBy(t => t.ParentTaskId == null ? 0 : 1))
        {
            var nt = new ProjectTask
            {
                ProjectId = copy.Id, MilestoneId = t.MilestoneId is { } mid && msMap.TryGetValue(mid, out var nm) ? nm : null, Number = await numbers.NextAsync("task", entity.Id, null, ct),
                Title = t.Title, Description = t.Description, Status = ProjectTaskStatus.Todo, Priority = t.Priority, EstimatedHours = t.EstimatedHours, ActualHours = 0,
                AssignedTo = i.IncludeTeam ? t.AssignedTo : null, AssignedUserId = i.IncludeTeam ? t.AssignedUserId : null, StartDate = Move(t.StartDate), DueDate = Move(t.DueDate), Labels = t.Labels,
                ParentTaskId = t.ParentTaskId is { } pid && taskMap.TryGetValue(pid, out var np) ? np : null,
            };
            tasks.Add(nt); taskMap[t.Id] = nt.Id;
        }
        var oldIds = srcTasks.Select(t => t.Id).ToList();
        foreach (var d in await dependencies.Query(new Specification<ProjectTaskDependency>(d => oldIds.Contains(d.TaskId))).ToListAsync(ct))
            if (taskMap.TryGetValue(d.TaskId, out var a) && taskMap.TryGetValue(d.DependsOnTaskId, out var b)) dependencies.Add(new ProjectTaskDependency { TaskId = a, DependsOnTaskId = b });
        if (i.IncludeTeam)
            foreach (var m in await members.Query(new Specification<ProjectMember>(x => x.ProjectId == src.Id)).ToListAsync(ct))
                members.Add(new ProjectMember { ProjectId = copy.Id, UserId = m.UserId, Name = m.Name, Role = m.Role });
        foreach (var r in await rates.Query(new Specification<ProjectRate>(x => x.ProjectId == src.Id)).ToListAsync(ct))
            rates.Add(new ProjectRate { ProjectId = copy.Id, UserId = r.UserId, Role = r.Role, BillRate = r.BillRate, CostRate = r.CostRate });
        await projects.SaveChangesAsync(ct);
        return await sender.Send(new GetProject(copy.Id), ct);
    }

    public async Task<ProjectDto> Handle(ArchiveProject cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        if (cmd.Archive && p.Status is ProjectStatus.Active or ProjectStatus.OnHold) throw new ConflictException("Complete or cancel the project before archiving it.");
        p.IsArchived = cmd.Archive;
        await projects.SaveChangesAsync(ct);
        return await sender.Send(new GetProject(p.Id), ct);
    }
}

/// <summary>Internal: the name of a customer record.</summary>
public sealed record LookupCustomerName(Guid PartyId) : IQuery<string?>;

internal sealed class LookupCustomerNameHandler(IReadRepository<ERP.Domain.Parties.Party> parties) : IRequestHandler<LookupCustomerName, string?>
{
    public async Task<string?> Handle(LookupCustomerName q, CancellationToken ct) => (await parties.FindAsync(q.PartyId, ct))?.Name;
}
