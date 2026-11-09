using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Replaces a budget's fields and lines. Its legal entity cannot change.</summary>
public sealed record UpdateBudgetCommand(Guid Id, BudgetInput Input) : ICommand<BudgetDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BudgetsWrite;
}
