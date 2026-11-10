using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Saves the current plan of every task as the baseline to compare against; clear removes it.</summary>
public sealed record SetProjectBaselineCommand(Guid ProjectId, bool Clear) : ICommand<ScheduleDto>, IRequirePermission
{
    public string RequiredPermission => P.TasksManage;
}
