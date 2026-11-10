using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A vendor's open bills as of a date, with its unallocated payments (advances) and the balance.</summary>
public sealed record GetVendorStatementQuery(Guid PartyId, DateOnly AsOf, Guid? LegalEntityId) : IQuery<VendorStatementDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
