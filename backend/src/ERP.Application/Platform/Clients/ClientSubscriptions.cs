using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Loads a client's subscription for a change, and saves it the same way for every client command.</summary>
internal sealed class ClientSubscriptions(IWorkspaceSubscriptionRepository subscriptions, IOrganizationRepository organizations, ISubscriptionAccess access, ClientReader reader)
{
    /// <summary>The client and its subscription, tracked. The platform's own workspace and a client without a plan are refused.</summary>
    public async Task<(Organization Org, WorkspaceSubscription Sub)> LoadAsync(Guid orgId, CancellationToken ct)
    {
        var org = await organizations.GetAsync(orgId, ct);
        if (org.IsInternal) throw new ConflictException("The platform's own workspace has no subscription.");
        var sub = await subscriptions.FindForClientForUpdateAsync(orgId, ct) ?? throw new ConflictException("This client has no plan yet. Assign one first.");
        return (org, sub);
    }

    /// <summary>Saves, drops the cached access level of the client and returns its page.</summary>
    public async Task<ClientDetail> SavedAsync(Guid orgId, CancellationToken ct)
    {
        await subscriptions.SaveChangesAsync(ct);
        access.Invalidate(orgId);
        return await reader.DetailAsync(orgId, ct);
    }
}
