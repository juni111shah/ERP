using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Finance;

/// <summary>Payables by vendor in buckets of days past due. Unallocated payments (vendor advances) reduce what is owed.</summary>
public sealed record GetApAgeingQuery(DateOnly AsOf, Guid? LegalEntityId) : IQuery<ArAgeingDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ReportsRead;
}
