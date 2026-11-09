using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Hours logged and planned per person in a period, against their capacity.</summary>
public sealed record GetUtilisationReportQuery(DateOnly From, DateOnly To) : IQuery<IReadOnlyList<UtilisationRow>>, IRequirePermission
{
    public string RequiredPermission => P.ReportsRead;
}
