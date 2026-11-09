using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the task view: the task with its project name, milestone title and dependencies. Shared by the task handlers.</summary>
public sealed class TaskDtoBuilder(IProjectRepository projects, IProjectMilestoneRepository milestones, IProjectTaskDependencyRepository dependencies)
{
    public async Task<TaskDto> BuildAsync(ProjectTask t, CancellationToken ct)
    {
        var p = await projects.GetAsync(t.ProjectId, ct);
        string? mTitle = null;
        if (t.MilestoneId is { } mid) mTitle = await milestones.GetTitleAsync(mid, ct);
        var id = t.Id;
        var deps = await dependencies.ListDependsOnAsync(id, ct);
        return new TaskDto(t.Id, t.ProjectId, p.Name, t.MilestoneId, mTitle, t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssignedTo, t.EstimatedHours, t.ActualHours, t.DueDate, t.CreatedAt,
            t.AssignedUserId, t.ParentTaskId, t.StartDate, t.Labels, deps);
    }
}
