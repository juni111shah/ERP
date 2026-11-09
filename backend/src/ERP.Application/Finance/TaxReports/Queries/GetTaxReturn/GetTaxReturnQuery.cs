using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// The periodic tax return of a legal entity, laid out by its country pack. Sales are issued invoices and credit notes (credit notes negative);
/// purchases are posted bills. A legal entity is required because the country, and so the layout, comes from it.
/// </summary>
public sealed record GetTaxReturnQuery(DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<TaxReturnDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
