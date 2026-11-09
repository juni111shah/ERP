using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Takes the cheque to the bank. A post-dated cheque cannot be deposited before the date written on it.</summary>
public sealed record DepositChequeCommand(Guid Id, Guid BankAccountId, DateOnly Date) : ICommand<ChequeDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesApprove;
}
