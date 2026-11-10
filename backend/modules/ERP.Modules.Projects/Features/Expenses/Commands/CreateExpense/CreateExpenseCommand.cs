using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Records an expense on a project; it starts as a draft when expenses need approval.</summary>
public sealed record CreateExpenseCommand(ExpenseInput Input) : ICommand<ExpenseDto>, IRequirePermission
{
    public string RequiredPermission => P.ExpensesManage;
}
