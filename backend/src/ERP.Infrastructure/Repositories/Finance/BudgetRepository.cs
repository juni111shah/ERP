using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class BudgetRepository(IRepository<Budget> budgets) : RepositoryBase<Budget>(budgets), IBudgetRepository
{
    public Task<Guid?> FindFirstActiveIdAsync(int fiscalYear, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Budget>(b => b.IsActive && b.FiscalYear == fiscalYear && (legalEntityId == null || b.LegalEntityId == legalEntityId)))
            .OrderBy(b => b.Name).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
}
