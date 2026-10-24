using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

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
    DateOnly? DueDate);

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
    DateTime CreatedAt);

public sealed record GetTask(Guid Id) : IQuery<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksRead; }
public sealed record CreateTask(TaskInput Input) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record UpdateTask(Guid Id, TaskInput Input) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record DeleteTask(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.TasksManage; }
public sealed record ChangeTaskStatus(Guid Id, ProjectTaskStatus Status) : ICommand<TaskDto>, IRequirePermission { public string RequiredPermission => P.TasksManage; }

public sealed class TaskInputValidator : AbstractValidator<TaskInput>
{
    public TaskInputValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.AssignedTo).MaximumLength(100);
        RuleFor(x => x.EstimatedHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class CreateTaskValidator : AbstractValidator<CreateTask>
{
    public CreateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator());
}

public sealed class UpdateTaskValidator : AbstractValidator<UpdateTask>
{
    public UpdateTaskValidator() => RuleFor(x => x.Input).SetValidator(new TaskInputValidator());
}

internal sealed class TaskHandlers(
    IRepository<ProjectTask> tasks,
    IReadRepository<Project> projects,
    IReadRepository<ProjectMilestone> milestones,
    INumberingService numbers) :
    IRequestHandler<GetTask, TaskDto>,
    IRequestHandler<CreateTask, TaskDto>,
    IRequestHandler<UpdateTask, TaskDto>,
    IRequestHandler<DeleteTask, Unit>,
    IRequestHandler<ChangeTaskStatus, TaskDto>
{
    public async Task<TaskDto> Handle(GetTask q, CancellationToken ct)
    {
        var t = await tasks.GetAsync(q.Id, ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<TaskDto> Handle(CreateTask cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var p = await projects.GetAsync(i.ProjectId, ct);
        var t = new ProjectTask
        {
            ProjectId = p.Id,
            MilestoneId = i.MilestoneId,
            Title = i.Title.Trim(),
            Description = i.Description?.Trim(),
            Status = i.Status,
            Priority = i.Priority,
            AssignedTo = i.AssignedTo?.Trim(),
            EstimatedHours = i.EstimatedHours,
            ActualHours = i.ActualHours,
            DueDate = i.DueDate,
            Number = await numbers.NextAsync("task", p.LegalEntityId, null, ct),
        };
        tasks.Add(t);
        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<TaskDto> Handle(UpdateTask cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        t.MilestoneId = i.MilestoneId;
        t.Title = i.Title.Trim();
        t.Description = i.Description?.Trim();
        t.Status = i.Status;
        t.Priority = i.Priority;
        t.AssignedTo = i.AssignedTo?.Trim();
        t.EstimatedHours = i.EstimatedHours;
        t.ActualHours = i.ActualHours;
        t.DueDate = i.DueDate;

        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    public async Task<Unit> Handle(DeleteTask cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        tasks.Remove(t);
        await tasks.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<TaskDto> Handle(ChangeTaskStatus cmd, CancellationToken ct)
    {
        var t = await tasks.GetForUpdateAsync(cmd.Id, ct);
        t.Status = cmd.Status;
        await tasks.SaveChangesAsync(ct);
        return await BuildDtoAsync(t, ct);
    }

    private async Task<TaskDto> BuildDtoAsync(ProjectTask t, CancellationToken ct)
    {
        var p = await projects.GetAsync(t.ProjectId, ct);
        string? mTitle = null;
        if (t.MilestoneId.HasValue)
        {
            var m = await milestones.Query(new Specification<ProjectMilestone>(x => x.Id == t.MilestoneId.Value)).FirstOrDefaultAsync(ct);
            mTitle = m?.Title;
        }

        return new TaskDto(
            t.Id,
            t.ProjectId,
            p.Name,
            t.MilestoneId,
            mTitle,
            t.Number,
            t.Title,
            t.Description,
            t.Status,
            t.Priority,
            t.AssignedTo,
            t.EstimatedHours,
            t.ActualHours,
            t.DueDate,
            t.CreatedAt);
    }
}
