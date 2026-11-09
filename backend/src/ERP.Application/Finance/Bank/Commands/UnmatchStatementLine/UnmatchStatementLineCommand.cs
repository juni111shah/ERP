using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Removes the match of a statement line (a journal it was booked to stays).</summary>
public sealed record UnmatchStatementLineCommand(Guid LineId) : ICommand<StatementLineDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
