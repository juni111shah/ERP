using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Budget against cost, forecast, earned value and health of every live project.</summary>
public sealed record GetBudgetReportQuery() : IQuery<IReadOnlyList<BudgetRow>>, IRequirePermission
{
    public string RequiredPermission => P.ReportsRead;
}
