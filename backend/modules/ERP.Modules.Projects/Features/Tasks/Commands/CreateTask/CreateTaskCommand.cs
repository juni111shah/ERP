using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Adds a task (or a subtask) to an open project, numbered from the task sequence.</summary>
public sealed record CreateTaskCommand(TaskInput Input) : ICommand<TaskDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
