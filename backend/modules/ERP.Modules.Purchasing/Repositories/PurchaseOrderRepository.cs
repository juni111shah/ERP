using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchaseOrderRepository(IRepository<PurchaseOrder> inner) : RepositoryBase<PurchaseOrder>(inner), IPurchaseOrderRepository
{
    public Task<PurchaseOrder?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PurchaseOrder>(x => x.Id == id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

    public async Task<decimal> TotalNotCancelledAsync(CancellationToken ct)
        => await Query().Where(o => o.Status != PurchaseOrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

    public Task<int> CountOpenAsync(CancellationToken ct) => Open().CountAsync(ct);

    public async Task<decimal> TotalOpenAsync(CancellationToken ct) => await Open().SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

    private IQueryable<PurchaseOrder> Open() => Query().Where(o => o.Status == PurchaseOrderStatus.Draft
        || o.Status == PurchaseOrderStatus.Submitted
        || o.Status == PurchaseOrderStatus.Approved
        || o.Status == PurchaseOrderStatus.PartiallyReceived);
}
