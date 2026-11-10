using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>Data access for subscription plans. Implemented in ERP.Infrastructure/Repositories/Platform/SubscriptionPlanRepository.cs.</summary>
public interface ISubscriptionPlanRepository : IRepository<SubscriptionPlan>
{
    /// <summary>The active public plans in price-list order, read-only.</summary>
    Task<List<SubscriptionPlan>> ListPublicAsync(CancellationToken ct);

    /// <summary>The plan when it is active and public, otherwise null.</summary>
    Task<SubscriptionPlan?> FindPublicAsync(Guid id, CancellationToken ct);

    /// <summary>The longest free trial of the active public plans, or null when there is none.</summary>
    Task<int?> LongestPublicTrialAsync(CancellationToken ct);

    /// <summary>Every plan, archived ones included, in price-list order.</summary>
    Task<List<SubscriptionPlan>> ListInOrderAsync(CancellationToken ct);

    /// <summary>Every plan by id.</summary>
    Task<Dictionary<Guid, SubscriptionPlan>> ByIdAsync(CancellationToken ct);

    /// <summary>Every plan's name by id.</summary>
    Task<Dictionary<Guid, string>> NamesByIdAsync(CancellationToken ct);
}
