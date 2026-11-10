using System.Linq.Expressions;
using ERP.Modules.Projects.Domain;

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
    IReadOnlyList<Guid>? DependsOn = null)
{
    /// <summary>The list projection (translated to SQL); names are filled in afterwards by <see cref="ProjectListEnrich.Tasks"/>.</summary>
    public static readonly Expression<Func<ProjectTask, TaskDto>> Projection = t => new TaskDto(
        t.Id, t.ProjectId, "", t.MilestoneId, null, t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssignedTo, t.EstimatedHours, t.ActualHours, t.DueDate, t.CreatedAt, t.AssignedUserId, t.ParentTaskId, t.StartDate, t.Labels, null);
}
