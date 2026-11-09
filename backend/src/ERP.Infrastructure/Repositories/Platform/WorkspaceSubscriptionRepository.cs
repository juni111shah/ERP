using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class WorkspaceSubscriptionRepository(IRepository<WorkspaceSubscription> subscriptions) : RepositoryBase<WorkspaceSubscription>(subscriptions), IWorkspaceSubscriptionRepository
{
    public Task<WorkspaceSubscription?> FindForClientAsync(Guid orgId, CancellationToken ct) => Query().FirstOrDefaultAsync(s => s.OrgId == orgId, ct);

    public Task<WorkspaceSubscription?> FindForClientForUpdateAsync(Guid orgId, CancellationToken ct) => Tracked().FirstOrDefaultAsync(s => s.OrgId == orgId, ct);

    public Task<Dictionary<Guid, WorkspaceSubscription>> ByOrgAsync(CancellationToken ct) => Query().ToDictionaryAsync(s => s.OrgId, ct);

    public Task<Dictionary<Guid, int>> CountRunningByPlanAsync(CancellationToken ct)
        => Query().Where(s => s.Status != SubscriptionStatus.Cancelled).GroupBy(s => s.PlanId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}
