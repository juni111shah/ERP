using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for the platform's activity log. Implemented in ERP.Infrastructure/Repositories/Platform/PlatformActivityRepository.cs.</summary>
public interface IPlatformActivityRepository : IRepository<PlatformActivity>
{
    /// <summary>The newest <paramref name="take"/> entries of the whole platform.</summary>
    Task<List<PlatformActivity>> ListLatestAsync(int take, CancellationToken ct);

    /// <summary>The newest <paramref name="take"/> entries about one client.</summary>
    Task<List<PlatformActivity>> ListLatestForClientAsync(Guid orgId, int take, CancellationToken ct);

    /// <summary>One page of the log, newest first, optionally for one client and matching a search, with the total count.</summary>
    Task<(List<PlatformActivity> Rows, int Total)> ListPageAsync(Guid? orgId, string? search, int page, int size, CancellationToken ct);
}
