using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Logs time on a project; the rates come from the rate cards unless given.</summary>
public sealed record CreateTimesheetCommand(TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsManage;
}
