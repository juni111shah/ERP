using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Work ready to bill per project, aged in 30-day buckets as of a date (today by default).</summary>
public sealed record GetUnbilledAgeingQuery(DateOnly? AsOf) : IQuery<IReadOnlyList<AgeingRow>>, IRequirePermission
{
    public string RequiredPermission => P.ReportsRead;
}
