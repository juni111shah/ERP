using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Creates, changes or removes (Hours 0 with an Id) many entries at once, for the weekly grid.</summary>
public sealed record SaveTimesheetWeekCommand(IReadOnlyList<TimesheetInput> Items) : ICommand<IReadOnlyList<TimesheetDto>>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsManage;
}
