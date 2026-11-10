using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Tax on issued invoices and credit notes (output) against recoverable tax on posted bills (input) for a period. The per-country return layout is a country pack concern; this is the neutral base.</summary>
public sealed record GetTaxSummaryQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<TaxSummaryDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
