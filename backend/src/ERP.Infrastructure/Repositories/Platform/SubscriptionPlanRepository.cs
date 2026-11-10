using ERP.Application.Abstractions;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Platform;

internal sealed class SubscriptionPlanRepository(IRepository<SubscriptionPlan> plans) : RepositoryBase<SubscriptionPlan>(plans), ISubscriptionPlanRepository
{
    public Task<List<SubscriptionPlan>> ListPublicAsync(CancellationToken ct)
        => Query().Where(p => p.IsActive && p.IsPublic).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

    public Task<SubscriptionPlan?> FindPublicAsync(Guid id, CancellationToken ct)
        => Query().FirstOrDefaultAsync(p => p.Id == id && p.IsActive && p.IsPublic, ct);

    public Task<int?> LongestPublicTrialAsync(CancellationToken ct)
        => Query().Where(p => p.IsActive && p.IsPublic).MaxAsync(p => (int?)p.TrialDays, ct);

    public Task<List<SubscriptionPlan>> ListInOrderAsync(CancellationToken ct) => Query().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

    public Task<Dictionary<Guid, SubscriptionPlan>> ByIdAsync(CancellationToken ct) => Query().ToDictionaryAsync(p => p.Id, ct);

    public Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct) => Query().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
}
