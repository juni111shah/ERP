using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Adds bank statement lines to a bank account. Lines already imported (same date, amount, reference and text) are skipped.</summary>
public sealed record ImportStatementCommand(Guid LegalEntityId, Guid AccountId, List<StatementLineInput> Lines) : ICommand<ImportResult>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankWrite;
}
