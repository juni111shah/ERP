using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits a task of an open project.</summary>
public sealed record UpdateTaskCommand(Guid Id, TaskInput Input) : ICommand<TaskDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
