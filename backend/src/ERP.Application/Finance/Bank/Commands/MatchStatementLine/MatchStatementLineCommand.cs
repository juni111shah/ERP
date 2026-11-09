using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Matches one statement line to an unmatched ledger line of the same amount on the same bank account.</summary>
public sealed record MatchStatementLineCommand(Guid LineId, Guid JournalLineId) : ICommand<StatementLineDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
