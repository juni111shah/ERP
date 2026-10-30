using ERP.Application.Common;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public static class ProjectsTransitions
{
    public static readonly StatusTransitions<ProjectStatus> Project = new(
        new Dictionary<ProjectStatus, IEnumerable<ProjectStatus>>
        {
            [ProjectStatus.Planning] = [ProjectStatus.Active, ProjectStatus.Cancelled],
            [ProjectStatus.Active] = [ProjectStatus.OnHold, ProjectStatus.Completed, ProjectStatus.Cancelled],
            [ProjectStatus.OnHold] = [ProjectStatus.Active, ProjectStatus.Cancelled],
            [ProjectStatus.Completed] = [],
            [ProjectStatus.Cancelled] = [],
        });

    public static readonly StatusTransitions<MilestoneStatus> Milestone = new(
        new Dictionary<MilestoneStatus, IEnumerable<MilestoneStatus>>
        {
            [MilestoneStatus.Pending] = [MilestoneStatus.Achieved],
            [MilestoneStatus.Achieved] = [MilestoneStatus.Pending, MilestoneStatus.Invoiced],
            [MilestoneStatus.Invoiced] = [],
        },
        systemOnly: [MilestoneStatus.Invoiced]);

    public static readonly StatusTransitions<ProjectTaskStatus> Task = new(
        new Dictionary<ProjectTaskStatus, IEnumerable<ProjectTaskStatus>>
        {
            [ProjectTaskStatus.Todo] = [ProjectTaskStatus.InProgress],
            [ProjectTaskStatus.InProgress] = [ProjectTaskStatus.Review, ProjectTaskStatus.Todo],
            [ProjectTaskStatus.Review] = [ProjectTaskStatus.Done, ProjectTaskStatus.InProgress],
            [ProjectTaskStatus.Done] = [ProjectTaskStatus.InProgress],
        });
}
