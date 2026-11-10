using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>
/// Receivables by customer, in buckets of days past due. Credit notes and unallocated receipts reduce the balance.
/// Optional filters (business unit, source type, party group) and grouping (<c>customer</c> default, <c>businessUnit</c>, <c>sourceType</c> or <c>partyGroup</c>). Receipts on account belong to a customer (so to its group) but carry no business unit or source: they appear unless a business unit or source filter is set, and only in customer or party-group rows.
/// </summary>
public sealed record GetArAgeingQuery(DateOnly AsOf, Guid? LegalEntityId, Guid? BusinessUnitId = null, string? SourceType = null, string? GroupBy = null, Guid? PartyGroupId = null) : IQuery<ArAgeingDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
