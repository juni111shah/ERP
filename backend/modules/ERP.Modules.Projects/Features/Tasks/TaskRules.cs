using ERP.Application.Common;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Checks and form handling shared by the task handlers.</summary>
public sealed class TaskRules(
    IProjectTaskRepository tasks,
    IProjectMilestoneRepository milestones,
    IProjectTaskDependencyRepository dependencies,
    ProjectPeople people)
{
    public async Task FillAsync(ProjectTask t, TaskInput i, CancellationToken ct)
    {
        string? assignee = i.AssignedTo?.Trim();
        if (i.AssignedUserId is { } uid) assignee = await people.RequireNameAsync(uid, "assignedUserId", ct);
        t.MilestoneId = i.MilestoneId; t.Title = i.Title.Trim(); t.Description = i.Description?.Trim(); t.Status = i.Status; t.Priority = i.Priority;
        t.AssignedTo = assignee; t.AssignedUserId = i.AssignedUserId; t.EstimatedHours = i.EstimatedHours; t.DueDate = i.DueDate;
        t.ParentTaskId = i.ParentTaskId; t.StartDate = i.StartDate; t.Labels = string.IsNullOrWhiteSpace(i.Labels) ? null : string.Join(',', i.Labels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Allowed moves, and: a task is Done only when what it waits for and its subtasks are finished.</summary>
    public async Task CheckStatusAsync(ProjectTask t, ProjectTaskStatus to, CancellationToken ct)
    {
        ProjectsTransitions.Task.Ensure(t.Status, to, isSystem: false);
        if (to != ProjectTaskStatus.Done) return;
        var id = t.Id;
        var waiting = await dependencies.ListDependsOnAsync(id, ct);
        if (waiting.Count > 0)
        {
            var open = await tasks.ListOpenNumbersAsync(waiting, ct);
            if (open.Count > 0) throw new ConflictException($"Finish {string.Join(", ", open)} first; this task waits for it.");
        }
        var openSubs = await tasks.CountOpenSubtasksAsync(id, ct);
        if (openSubs > 0) throw new ConflictException($"{openSubs} subtask(s) are still open.");
    }

    public async Task EnsureMilestoneInProjectAsync(Guid? milestoneId, Guid projectId, CancellationToken ct)
    {
        if (milestoneId is { } mid && !await milestones.AnyAsync(m => m.Id == mid && m.ProjectId == projectId, ct))
            throw new FieldValidationException("milestoneId", "Milestone not found in this project.");
    }

    public async Task EnsureParentAsync(Guid? parentId, Guid projectId, Guid? selfId, CancellationToken ct)
    {
        if (parentId is not { } pid) return;
        if (pid == selfId) throw new FieldValidationException("parentTaskId", "A task cannot be its own parent.");
        var parent = await tasks.FindParentLinkAsync(pid, projectId, ct)
            ?? throw new FieldValidationException("parentTaskId", "Parent task not found in this project.");
        if (parent.ParentTaskId is not null) throw new FieldValidationException("parentTaskId", "Subtasks can only go one level deep.");
        if (selfId is { } sid && await tasks.AnyAsync(x => x.ParentTaskId == sid, ct)) throw new FieldValidationException("parentTaskId", "A task that has subtasks cannot become a subtask.");
    }
}
