using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Moves a task to another status (the board).</summary>
public sealed record ChangeTaskStatusCommand(Guid Id, ProjectTaskStatus Status) : ICommand<TaskDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
