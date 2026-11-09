using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>One time entry.</summary>
public sealed record GetTimesheetQuery(Guid Id) : IQuery<TimesheetDto>, IRequirePermission
{
    public string RequiredPermission => P.TimesheetsRead;
}
