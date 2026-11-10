using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Deletes a budget.</summary>
public sealed record DeleteBudgetCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BudgetsWrite;
}
