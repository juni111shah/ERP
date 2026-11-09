using ERP.Application.Abstractions;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Repositories;

internal sealed class SalesQuoteRepository(IRepository<SalesQuote> inner) : RepositoryBase<SalesQuote>(inner), ISalesQuoteRepository
{
    public Task<SalesQuote?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<SalesQuote>(x => x.Id == id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountOpenAsync(CancellationToken ct) => Open().CountAsync(ct);

    public async Task<decimal> TotalOpenAsync(CancellationToken ct) => await Open().SumAsync(q => (decimal?)q.TotalAmount, ct) ?? 0m;

    private IQueryable<SalesQuote> Open() => Query().Where(q => q.Status == SalesQuoteStatus.Draft || q.Status == SalesQuoteStatus.Sent);
}
