using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>One project expense.</summary>
public sealed record GetExpenseQuery(Guid Id) : IQuery<ExpenseDto>, IRequirePermission
{
    public string RequiredPermission => P.ExpensesRead;
}
