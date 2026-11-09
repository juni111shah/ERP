using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Matches unmatched statement lines to ledger lines with the same amount within five days.</summary>
public sealed record AutoMatchStatementCommand(Guid LegalEntityId, Guid AccountId) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
