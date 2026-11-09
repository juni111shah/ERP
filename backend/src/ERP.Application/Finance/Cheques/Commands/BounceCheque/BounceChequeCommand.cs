using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The bank returned it: the receipt is reversed and the invoices it settled are open again.</summary>
public sealed record BounceChequeCommand(Guid Id, DateOnly Date, string Reason) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesApprove;
}
