using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class BudgetRepository(IRepository<Budget> budgets) : RepositoryBase<Budget>(budgets), IBudgetRepository
{
    public async Task<Budget> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(WithLines(id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Budget> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(WithLines(id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    private static Specification<Budget> WithLines(Guid id) => new Specification<Budget>(b => b.Id == id).Include(b => b.Lines);

    public Task<Guid?> FindFirstActiveIdAsync(int fiscalYear, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Budget>(b => b.IsActive && b.FiscalYear == fiscalYear && (legalEntityId == null || b.LegalEntityId == legalEntityId)))
            .OrderBy(b => b.Name).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
}
