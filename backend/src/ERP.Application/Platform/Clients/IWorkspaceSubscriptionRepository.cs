using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for clients' subscriptions (not org-scoped: always filter on the client). Implemented in ERP.Infrastructure/Repositories/Platform/WorkspaceSubscriptionRepository.cs.</summary>
public interface IWorkspaceSubscriptionRepository : IRepository<WorkspaceSubscription>
{
    /// <summary>The subscription of <paramref name="orgId"/>, read-only, or null.</summary>
    Task<WorkspaceSubscription?> FindForClientAsync(Guid orgId, CancellationToken ct);

    /// <summary>The subscription of <paramref name="orgId"/>, tracked for changes, or null.</summary>
    Task<WorkspaceSubscription?> FindForClientForUpdateAsync(Guid orgId, CancellationToken ct);

    /// <summary>Every subscription by client.</summary>
    Task<Dictionary<Guid, WorkspaceSubscription>> ByOrgAsync(CancellationToken ct);

    /// <summary>How many subscriptions that are not cancelled each plan has.</summary>
    Task<Dictionary<Guid, int>> CountRunningByPlanAsync(CancellationToken ct);
}
