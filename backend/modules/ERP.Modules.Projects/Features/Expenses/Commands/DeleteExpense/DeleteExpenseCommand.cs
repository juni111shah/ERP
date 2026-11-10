using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes an expense that is not billed, submitted or in the ledger.</summary>
public sealed record DeleteExpenseCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.ExpensesManage;
}
