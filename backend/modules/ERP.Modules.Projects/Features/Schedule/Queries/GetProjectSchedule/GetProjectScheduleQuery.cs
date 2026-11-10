using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>The project's tasks scheduled over their durations and dependencies, with the critical path and the milestones.</summary>
public sealed record GetProjectScheduleQuery(Guid ProjectId) : IQuery<ScheduleDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksRead;
}
