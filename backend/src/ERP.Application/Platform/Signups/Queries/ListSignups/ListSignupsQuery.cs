using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The registrations, newest first, optionally in one state.</summary>
public sealed record ListSignupsQuery : PagedQuery, IQuery<PagedResult<SignupRow>>
{
    public string? Status { get; init; }
}
