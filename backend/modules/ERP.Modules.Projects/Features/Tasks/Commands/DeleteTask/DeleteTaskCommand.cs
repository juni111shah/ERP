using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a task that has no time and no subtasks, with its dependencies.</summary>
public sealed record DeleteTaskCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
