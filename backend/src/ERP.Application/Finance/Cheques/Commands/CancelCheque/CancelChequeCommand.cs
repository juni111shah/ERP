using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Cancels a cheque still in custody: its receipt is reversed and the invoices it settled are open again.</summary>
public sealed record CancelChequeCommand(Guid Id) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesWrite;
}
