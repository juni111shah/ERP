using ERP.Application.Security;

namespace ERP.Application.Platform;

/// <summary>
/// Brings a client's open screens in line when the founder (or the daily billing run) changes how its subscription stands: suspended,
/// cancelled, view-only, back to normal, a trial extended. Take <see cref="BeforeAsync"/> before saving and pass it to
/// <see cref="AfterAsync"/> after: the cached access level is dropped, and when the client's standing really changed, its signed-in people
/// get "access-changed" and reload their session, so the blocked page, the banner or the normal workspace shows at once.
/// </summary>
public sealed class SubscriptionChanges(ISubscriptionAccess access, IAccessChangeNotifier notifier)
{
    public Task<SubscriptionSummary> BeforeAsync(Guid orgId, CancellationToken ct) => access.GetAsync(orgId, ct);

    /// <param name="modulesRemoved">A plan change switched modules off: always pushed, and the members' old tokens end (see <see cref="IAccessChangeNotifier"/>).</param>
    public async Task AfterAsync(Guid orgId, SubscriptionSummary before, CancellationToken ct, bool modulesRemoved = false)
    {
        access.Invalidate(orgId);
        if (modulesRemoved) await notifier.WorkspaceChangedAsync(orgId, AccessChangeReasons.PlanChanged, ct, narrowed: true);
        else if (await access.GetAsync(orgId, ct) != before) await notifier.WorkspaceChangedAsync(orgId, AccessChangeReasons.SubscriptionChanged, ct);
    }

    /// <summary>The standing is known to have changed (the billing run): drop the cached level and tell the client.</summary>
    public async Task ChangedAsync(Guid orgId, CancellationToken ct)
    {
        access.Invalidate(orgId);
        await notifier.WorkspaceChangedAsync(orgId, AccessChangeReasons.SubscriptionChanged, ct);
    }
}
