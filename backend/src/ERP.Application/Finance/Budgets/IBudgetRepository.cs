using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for budgets. Implemented in ERP.Infrastructure/Repositories/Finance/BudgetRepository.cs.</summary>
public interface IBudgetRepository : IRepository<Budget>
{
    /// <summary>The budget with its lines, read-only. Throws NotFoundException.</summary>
    Task<Budget> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The budget with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<Budget> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The id of the first (by name) active budget of <paramref name="fiscalYear"/>, of one legal entity or (null) any; null when there is none.</summary>
    Task<Guid?> FindFirstActiveIdAsync(int fiscalYear, Guid? legalEntityId, CancellationToken ct);
}
