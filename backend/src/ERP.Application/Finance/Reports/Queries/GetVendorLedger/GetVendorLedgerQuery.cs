using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>A vendor's statement of account for a date range (see <see cref="PartyLedgerDto"/>).</summary>
public sealed record GetVendorLedgerQuery(Guid PartyId, DateOnly From, DateOnly To, Guid? LegalEntityId) : IQuery<PartyLedgerDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
