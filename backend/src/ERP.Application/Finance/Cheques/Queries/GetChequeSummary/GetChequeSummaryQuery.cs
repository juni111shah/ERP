using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Totals of the cheques received as of a date: in custody, matured, due soon, deposited and bounced.</summary>
public sealed record GetChequeSummaryQuery(DateOnly AsOf) : IQuery<ChequeSummaryDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ChequesRead;
}
