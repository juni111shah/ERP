using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>The reconciliation of one bank account as of a date: the ledger balance and what is unmatched on each side.</summary>
public sealed record GetReconciliationQuery(Guid AccountId, Guid? LegalEntityId, DateOnly AsOf) : IQuery<ReconciliationDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BankRead;
}
