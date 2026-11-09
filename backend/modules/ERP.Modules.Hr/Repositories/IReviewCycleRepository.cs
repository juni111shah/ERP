using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for review cycles.</summary>
public interface IReviewCycleRepository : IRepository<ReviewCycle>
{
    /// <summary>The name of a review cycle; empty when not found.</summary>
    Task<string> GetNameAsync(Guid id, CancellationToken ct);
}
