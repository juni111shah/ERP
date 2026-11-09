using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits an expense that is not billed, written off or in the ledger; a rejected one goes back to draft.</summary>
public sealed record UpdateExpenseCommand(Guid Id, ExpenseInput Input) : ICommand<ExpenseDto>, IRequirePermission
{
    public string RequiredPermission => P.ExpensesManage;
}
