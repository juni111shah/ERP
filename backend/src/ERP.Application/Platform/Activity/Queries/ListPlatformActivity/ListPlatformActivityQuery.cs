using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The platform's activity log, newest first, optionally for one client.</summary>
public sealed record ListPlatformActivityQuery : PagedQuery, IQuery<PagedResult<ActivityRow>>
{
    public Guid? OrgId { get; init; }
}
