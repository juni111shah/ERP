using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Replaces the list of tasks that must finish before this one.</summary>
public sealed record SetTaskDependenciesCommand(Guid Id, IReadOnlyList<Guid> DependsOn) : ICommand<TaskDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
