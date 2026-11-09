using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A bounced cheque is replaced by a new one from the customer. Without allocations the replacement settles the invoices the bounced cheque had settled (the ones that are open again, oldest first).</summary>
public sealed record ReplaceChequeCommand(Guid Id, ChequeInput Replacement) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesApprove;
}
