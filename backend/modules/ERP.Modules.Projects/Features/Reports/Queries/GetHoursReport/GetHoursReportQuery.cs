using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Hours, value and cost of the time logged in a period, grouped by person, project or task.</summary>
public sealed record GetHoursReportQuery(DateOnly From, DateOnly To, string GroupBy, Guid? ProjectId) : IQuery<IReadOnlyList<HoursRow>>, IRequirePermission
{
    public string RequiredPermission => P.ReportsRead;
}
