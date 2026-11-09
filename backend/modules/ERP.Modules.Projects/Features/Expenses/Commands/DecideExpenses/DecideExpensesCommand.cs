using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Approves or rejects submitted expenses; answers how many were decided.</summary>
public sealed record DecideExpensesCommand(IReadOnlyList<Guid> Ids, bool Approve, string? Reason) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.ExpensesApprove;
}
