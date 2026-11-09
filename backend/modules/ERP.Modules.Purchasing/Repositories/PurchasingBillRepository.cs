using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchasingBillRepository(IRepository<Bill> inner) : RepositoryBase<Bill>(inner), IPurchasingBillRepository
{
    public Task<Bill?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<Bill>(b => b.Id == id)).FirstOrDefaultAsync(ct);

    public Task<Bill?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<Bill>(b => b.Id == id).Include(b => b.Lines)).FirstOrDefaultAsync(ct);
}
