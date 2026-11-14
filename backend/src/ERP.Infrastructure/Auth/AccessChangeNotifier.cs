using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Security;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Auth;

/// <summary>
/// Tells the affected members' open screens over SignalR and, when access was narrowed, ends their access tokens in that workspace (the
/// same cut-off a password change uses, but refresh tokens stay valid, so the session simply continues with the new permissions).
/// </summary>
public sealed class AccessChangeNotifier(
    AppDbContext db, IOrgContext org, IDistributedCache cache, IClock clock, IOptions<AuthOptions> options, INotificationPusher pusher,
    ILogger<AccessChangeNotifier> log) : IAccessChangeNotifier
{
    /// <summary>Client event name; the web and mobile apps refresh their session when they receive it.</summary>
    public const string EventName = "access-changed";

    public async Task UsersChangedAsync(Guid orgId, IEnumerable<Guid> userIds, string reason, bool narrowed, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;
        // 1. Something was taken away: tokens issued before now (to the millisecond) are refused; a refreshed token carries the new set.
        if (narrowed)
        {
            var cutoff = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            var ttl = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.AccessTokenMinutes + 1) };
            foreach (var id in ids) await cache.SetStringAsync(AuthService.CutoffKey(id, orgId), cutoff.ToString(), ttl, ct);
        }
        // 2. Open screens re-read the session at once. A lost push only delays this to the next API call or token refresh.
        await PushAsync(orgId, ids, reason, ct);
    }

    public async Task WorkspaceChangedAsync(Guid orgId, string reason, CancellationToken ct)
    {
        List<Guid> members;
        using (org.Use(orgId)) members = await db.OrganizationMembers.Where(m => m.IsActive).Select(m => m.UserId).ToListAsync(ct);
        await PushAsync(orgId, members, reason, ct);
    }

    private async Task PushAsync(Guid orgId, IReadOnlyList<Guid> userIds, string reason, CancellationToken ct)
    {
        foreach (var id in userIds)
        {
            try { await pusher.SendAsync(id, EventName, new { orgId, reason }, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogWarning(e, "Could not push {Event} to user {UserId}", EventName, id); }
        }
    }
}
