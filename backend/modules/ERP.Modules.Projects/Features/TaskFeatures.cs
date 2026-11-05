using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

/// <summary>ActualHours is ignored: it is always the sum of the time logged against the task.</summary>
public sealed record TaskInput(
    Guid ProjectId,
    Guid? MilestoneId,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    string? AssignedTo,
    decimal EstimatedHours,
    decimal ActualHours,
    DateOnly? DueDate,
    Guid? AssignedUserId = null,
    Guid? ParentTaskId = null,
    DateOnly? StartDate = null,
    string? Labels = null);

public sealed record TaskDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid? MilestoneId,
    string? MilestoneTitle,
    string Number,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    TaskPriority Priority,
    string? AssignedTo,
    decimal EstimatedHours,
    decimal ActualHours,
    DateOnly? DueDate,
    DateTime CreatedAt,
    Guid? AssignedUserId = null,
    Guid? ParentTaskId = null,
    DateOnly? StartDate = null,
    string? Labels = null,
    IReadOnlyList<Guid>? DependsOn = null);

public sealed record GetTask(Guid Id) : IQuery<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksRead; }
public sealed record CreateTask(TaskInput Input) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record UpdateTask(Guid Id, TaskInput Input) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record DeleteTask(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record ChangeTaskStatus(Guid Id, ProjectTaskStatus Status) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }
/// <summary>Replaces the list of tasks that must finish before this one.</summary>
public sealed record SetTaskDependencies(Guid Id, IReadOnlyList<Guid> DependsOn) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssignedTo).MaximumLength(100);
        RuleFor(x => x.Labels).MaximumLength(300);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x).Must(x => x.StartDate is null || x.DueDate is null || x.DueDate >= x.StartDate).WithName("dueDate").WithMessage("The due date cannot be before the start date.");
    }
}

public sealed class CreateTaskValidator : AbstractValidator<CreateTask> { public CreateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator()); }
public sealed class UpdateTaskValidator : AbstractValidator<UpdateTask> { public UpdateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator()); }
public sealed class SetTaskDependenciesValidator : AbstractValidator<SetTaskDependencies> { public SetTaskDependenciesValidator() => RuleFor(x => x.DependsOn).Must(d => d.Count <= 50).WithMessage("At most 50 dependencies."); }

/// <summary>Graph rules for tasks; no I/O.</summary>
public static class TaskGraph
{
    /// <summary>True when making <paramref name="taskId"/> depend on <paramref name="dependsOn"/> would create a loop.</summary>
    public static bool WouldLoop(Guid taskId, IEnumerable<Guid> dependsOn, IReadOnlyDictionary<Guid, List<Guid>> edges)
    {
        var seen = new HashSet<Guid>(); var stack = new Stack<Guid>(dependsOn);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n == taskId) return true;
            if (!seen.Add(n)) continue;
            if (edges.TryGetValue(n, out var next)) foreach (var m in next) stack.Push(m);
        }
        return false;
    }
}

internal sealed class TaskHandlers(
    IRepository<ProjectTask> tasks,
    IReadRepository<Project> projects,
    IReadRepository<ProjectMilestone> milestones,
    IReadRepository<ProjectTimesheet> timesheets,
    IRepository<ProjectTaskDependency> dependencies,
    ProjectPeople people,
    ProjectAccess access,
    INumberingService numbers) :
    IRequestHandler<GetTask, TaskDto>,
    IRequestHandler<CreateTask, TaskDto>,
    IRequestHandler<UpdateTask, TaskDto>,
    IRequestHandler<DeleteTask, Unit>,
    IRequestHandler<ChangeTaskStatus, TaskDto>,
    IRequestHandler<SetTaskDependencies, TaskDto>
{
    public async Task<TaskDto> Handle(GetTask q, CancellationToken ct) => await BuildDtoAsync(await tasks.GetAsync(q.Id, ct), ct);

    public async Task<TaskDto> Handle(CreateTask cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        ProjectRules.EnsureOpen(p);
        await access.EnsureAsync(p, ct);
        await EnsureMilestoneInProjectAsync(i.MilestoneId, p.Id, ct);
        await EnsureParentAsync(i.ParentTaskId, p.Id, null, ct);
        var t = new ProjectTask { ProjectId = p.Id, Number = await numbers.NextAsync("task", p.LegalEntityId, null, ct) };
        await FillAsync(t, i, ct);
        tasks.Add(t);
        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<TaskDto> Handle(UpdateTask cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        await EnsureMilestoneInProjectAsync(i.MilestoneId, t.ProjectId, ct);
        await EnsureParentAsync(i.ParentTaskId, t.ProjectId, t.Id, ct);
        if (i.Status != t.Status) await CheckStatusAsync(t, i.Status, ct);
        await FillAsync(t, i, ct);
        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<Unit> Handle(DeleteTask cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        var id = t.Id;
        if (await timesheets.AnyAsync(x => x.TaskId == id, ct)) throw new ConflictException("Time has been logged against this task. Cancel the task instead of deleting it.");
        if (await tasks.AnyAsync(x => x.ParentTaskId == id, ct)) throw new ConflictException("This task has subtasks. Delete or move them first.");
        foreach (var d in await dependencies.Tracked(new Specification<ProjectTaskDependency>(x => x.TaskId == id || x.DependsOnTaskId == id)).ToListAsync(ct)) dependencies.Remove(d);
        tasks.Remove(t);
        await tasks.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<TaskDto> Handle(ChangeTaskStatus cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        await CheckStatusAsync(t, cmd.Status, ct);
        t.Status = cmd.Status;
        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<TaskDto> Handle(SetTaskDependencies cmd, CancellationToken ct)
    {
        var t = await tasks.GetAsync(cmd.Id, ct);
        ProjectRules.EnsureOpen(await projects.GetAsync(t.ProjectId, ct));
        var wanted = cmd.DependsOn.Distinct().ToList();
        if (wanted.Contains(t.Id)) throw new FieldValidationException("dependsOn", "A task cannot depend on itself.");
        var projectTasks = await tasks.Query(new Specification<ProjectTask>(x => x.ProjectId == t.ProjectId)).Select(x => x.Id).ToListAsync(ct);
        if (wanted.Any(w => !projectTasks.Contains(w))) throw new FieldValidationException("dependsOn", "A task can only depend on tasks of the same project.");
        var edges = (await dependencies.Query(new Specification<ProjectTaskDependency>(x => projectTasks.Contains(x.TaskId))).ToListAsync(ct))
            .Where(x => x.TaskId != t.Id).GroupBy(x => x.TaskId).ToDictionary(g => g.Key, g => g.Select(x => x.DependsOnTaskId).ToList());
        if (TaskGraph.WouldLoop(t.Id, wanted, edges)) throw new ConflictException("Those dependencies would make the tasks wait for each other in a loop.");
        var existing = await dependencies.Tracked(new Specification<ProjectTaskDependency>(x => x.TaskId == t.Id)).ToListAsync(ct);
        foreach (var d in existing.Where(d => !wanted.Contains(d.DependsOnTaskId))) dependencies.Remove(d);
        foreach (var w in wanted.Where(w => existing.All(d => d.DependsOnTaskId != w))) dependencies.Add(new ProjectTaskDependency { TaskId = t.Id, DependsOnTaskId = w });
        await dependencies.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    private async Task FillAsync(ProjectTask t, TaskInput i, CancellationToken ct)
    {
        string? assignee = i.AssignedTo?.Trim();
        if (i.AssignedUserId is { } uid) assignee = await people.RequireNameAsync(uid, "assignedUserId", ct);
        t.MilestoneId = i.MilestoneId; t.Title = i.Title.Trim(); t.Description = i.Description?.Trim(); t.Status = i.Status; t.Priority = i.Priority;
        t.AssignedTo = assignee; t.AssignedUserId = i.AssignedUserId; t.EstimatedHours = i.EstimatedHours; t.DueDate = i.DueDate;
        t.ParentTaskId = i.ParentTaskId; t.StartDate = i.StartDate; t.Labels = string.IsNullOrWhiteSpace(i.Labels) ? null : string.Join(',', i.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Allowed moves, and: a task is Done only when what it waits for and its subtasks are finished.</summary>
    private async Task CheckStatusAsync(ProjectTask t, ProjectTaskStatus to, CancellationToken ct)
    {
        ProjectsTransitions.Task.Ensure(t.Status, to, isSystem: false);
        if (to != ProjectTaskStatus.Done) return;
        var id = t.Id;
        var waiting = await dependencies.Query(new Specification<ProjectTaskDependency>(d => d.TaskId == id)).Select(d => d.DependsOnTaskId).ToListAsync(ct);
        if (waiting.Count > 0)
        {
            var open = await tasks.Query(new Specification<ProjectTask>(x => waiting.Contains(x.Id) && x.Status != ProjectTaskStatus.Done && x.Status != ProjectTaskStatus.Cancelled)).Select(x => x.Number).ToListAsync(ct);
            if (open.Count > 0) throw new ConflictException($"Finish {string.Join(", ", open)} first; this task waits for it.");
        }
        var openSubs = await tasks.Query(new Specification<ProjectTask>(x => x.ParentTaskId == id && x.Status != ProjectTaskStatus.Done && x.Status != ProjectTaskStatus.Cancelled)).CountAsync(ct);
        if (openSubs > 0) throw new ConflictException($"{openSubs} subtask(s) are still open.");
    }

    private async Task EnsureMilestoneInProjectAsync(Guid? milestoneId, Guid projectId, CancellationToken ct)
    {
        if (milestoneId is { } mid && !await milestones.AnyAsync(m => m.Id == mid && m.ProjectId == projectId, ct))
            throw new FieldValidationException("milestoneId", "Milestone not found in this project.");
    }

    private async Task EnsureParentAsync(Guid? parentId, Guid projectId, Guid? selfId, CancellationToken ct)
    {
        if (parentId is not { } pid) return;
        if (pid == selfId) throw new FieldValidationException("parentTaskId", "A task cannot be its own parent.");
        var parent = await tasks.Query(new Specification<ProjectTask>(x => x.Id == pid && x.ProjectId == projectId)).Select(x => new { x.Id, x.ParentTaskId }).FirstOrDefaultAsync(ct)
            ?? throw new FieldValidationException("parentTaskId", "Parent task not found in this project.");
        if (parent.ParentTaskId is not null) throw new FieldValidationException("parentTaskId", "Subtasks can only go one level deep.");
        if (selfId is { } sid && await tasks.AnyAsync(x => x.ParentTaskId == sid, ct)) throw new FieldValidationException("parentTaskId", "A task that has subtasks cannot become a subtask.");
    }

    private async Task<TaskDto> BuildDtoAsync(ProjectTask t, CancellationToken ct)
    {
        var p = await projects.GetAsync(t.ProjectId, ct);
        string? mTitle = null;
        if (t.MilestoneId is { } mid) mTitle = await milestones.Query(new Specification<ProjectMilestone>(x => x.Id == mid)).Select(x => x.Title).FirstOrDefaultAsync(ct);
        var id = t.Id;
        var deps = await dependencies.Query(new Specification<ProjectTaskDependency>(d => d.TaskId == id)).Select(d => d.DependsOnTaskId).ToListAsync(ct);
        return new TaskDto(t.Id, t.ProjectId, p.Name, t.MilestoneId, mTitle, t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssignedTo, t.EstimatedHours, t.ActualHours, t.DueDate, t.CreatedAt,
            t.AssignedUserId, t.ParentTaskId, t.StartDate, t.Labels, deps);
    }
}
