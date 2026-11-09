using ERP.Application.Abstractions;
using ERP.Modules.Distribution.Domain;

namespace ERP.Modules.Distribution.Repositories;

/// <summary>Data access for wholesale price tiers. Implemented in WholesalePriceMatrixRepository.cs.</summary>
public interface IWholesalePriceMatrixRepository : IRepository<WholesalePriceMatrix>
{
    /// <summary>The active rule of the item and tier whose quantity band holds <paramref name="quantity"/>, the highest minimum first; null when none applies.</summary>
    Task<WholesalePriceMatrix?> FindMatchingRuleAsync(Guid legalEntityId, Guid itemId, WholesaleTier tier, decimal quantity, CancellationToken ct);

    /// <summary>The number of active rules.</summary>
    Task<int> CountActiveAsync(CancellationToken ct);
}
