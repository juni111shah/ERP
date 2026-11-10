using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Budget against the posted ledger for a range of months. Income variance is actual − budget; expense variance is budget − actual (positive = favourable).</summary>
public sealed record GetBudgetVsActualQuery(Guid BudgetId, int FromMonth, int ToMonth) : IQuery<BudgetVsActualDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BudgetsRead;
}
