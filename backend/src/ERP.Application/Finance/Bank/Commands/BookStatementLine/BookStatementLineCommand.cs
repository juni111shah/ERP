using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Books an unmatched statement line (a bank charge, interest...) against another account and matches it.</summary>
public sealed record BookStatementLineCommand(Guid LineId, Guid CounterAccountId, string? Memo) : ICommand<StatementLineDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
