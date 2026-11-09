using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a time entry that is not billed, submitted or in the ledger.</summary>
public sealed record DeleteTimesheetCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsManage;
}
