using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Moves money between two bank or cash accounts (Dr to, Cr from); answers the id of the journal.</summary>
public sealed record TransferFundsCommand(Guid LegalEntityId, Guid FromAccountId, Guid ToAccountId, decimal Amount, DateOnly Date, string? Memo) : ICommand<Guid>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
