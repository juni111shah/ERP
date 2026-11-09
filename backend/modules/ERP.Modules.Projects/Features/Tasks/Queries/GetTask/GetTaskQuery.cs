using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>One task with its project, milestone and dependencies.</summary>
public sealed record GetTaskQuery(Guid Id) : IQuery<TaskDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksRead;
}
