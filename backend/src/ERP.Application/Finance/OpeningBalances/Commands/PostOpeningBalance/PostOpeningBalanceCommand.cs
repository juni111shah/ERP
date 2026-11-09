using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// Posts the opening entry of a legal entity: the balances given, with the difference to the opening balance equity account. Receivable and payable lines also
/// raise an open invoice or bill, because that is what the ageing, the statements and later receipts and payments work from, so the subledger ties to the control account.
/// Refused while an earlier opening entry of the entity stands; reverse that one first.
/// </summary>
public sealed record PostOpeningBalanceCommand(OpeningBalanceInput Input) : ICommand<OpeningBalanceDto>, IRetryOnConcurrency, IRequirePermission
{
    public string RequiredPermission => CorePermissions.JournalsPost;
}
