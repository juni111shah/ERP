using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Invoices every scheduled instalment of an active lease that has fallen due by the given date (one invoice per instalment, posted to the ledger).</summary>
public sealed record RunBillingCommand(DateOnly AsOf, Guid? LegalEntityId) : ICommand<BillingResult>, IRequirePermission
{
    public string RequiredPermission => P.BillingRun;
}
