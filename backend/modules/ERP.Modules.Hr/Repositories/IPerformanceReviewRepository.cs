using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Repositories;

/// <summary>Data access for performance reviews.</summary>
public interface IPerformanceReviewRepository : IRepository<PerformanceReview>
{
    /// <summary>The review with its competencies, read-only. Throws NotFoundException.</summary>
    Task<PerformanceReview> GetWithCompetenciesAsync(Guid id, CancellationToken ct);

    /// <summary>The review with its competencies, tracked for changes. Throws NotFoundException.</summary>
    Task<PerformanceReview> GetWithCompetenciesForUpdateAsync(Guid id, CancellationToken ct);
}
