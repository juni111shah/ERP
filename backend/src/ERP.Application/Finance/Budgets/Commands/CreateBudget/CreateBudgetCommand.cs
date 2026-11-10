using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Adds a budget for a legal entity and fiscal year; its name is unique within that year.</summary>
public sealed record CreateBudgetCommand(BudgetInput Input) : ICommand<BudgetDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BudgetsWrite;
}
