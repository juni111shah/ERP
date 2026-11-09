using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits a time entry that is not billed, written off or in the ledger.</summary>
public sealed record UpdateTimesheetCommand(Guid Id, TimesheetInput Input) : ICommand<TimesheetDto>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsManage;
}
