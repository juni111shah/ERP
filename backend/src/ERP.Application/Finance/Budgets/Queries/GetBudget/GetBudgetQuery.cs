using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>One budget with its lines and totals.</summary>
public sealed record GetBudgetQuery(Guid Id) : IQuery<BudgetDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BudgetsRead;
}
