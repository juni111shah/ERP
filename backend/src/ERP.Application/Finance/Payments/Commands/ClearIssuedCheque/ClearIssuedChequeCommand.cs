using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The vendor banked a cheque we issued: Dr cheques issued, Cr bank.</summary>
public sealed record ClearIssuedChequeCommand(Guid InstrumentId, DateOnly Date) : ICommand<PaymentDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesApprove;
}
