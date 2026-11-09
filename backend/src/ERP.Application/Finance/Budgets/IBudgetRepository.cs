using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for budgets. Implemented in ERP.Infrastructure/Repositories/Finance/BudgetRepository.cs.</summary>
public interface IBudgetRepository : IRepository<Budget>
{
    /// <summary>The id of the first (by name) active budget of <paramref name="fiscalYear"/>, of one legal entity or (null) any; null when there is none.</summary>
    Task<Guid?> FindFirstActiveIdAsync(int fiscalYear, Guid? legalEntityId, CancellationToken ct);
}
