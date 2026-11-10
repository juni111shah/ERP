using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class PurchaseOrderBillLineRepository(IRepository<PurchaseOrderBillLine> inner) : RepositoryBase<PurchaseOrderBillLine>(inner), IPurchaseOrderBillLineRepository
{
    public Task<List<Guid>> ListBillIdsOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<PurchaseOrderBillLine>(r => r.OrderId == orderId)).OrderBy(r => r.CreatedAt).Select(r => r.BillId).Distinct().ToListAsync(ct);

    public Task<List<Guid>> ListDistinctBillIdsOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<PurchaseOrderBillLine>(r => r.OrderId == orderId)).Select(r => r.BillId).Distinct().ToListAsync(ct);

    public Task<List<PurchaseOrderBillLine>> ListForOrderLineForUpdateAsync(Guid orderLineId, Guid? billId, CancellationToken ct)
        => Tracked(new Specification<PurchaseOrderBillLine>(r => r.OrderLineId == orderLineId && (billId == null || r.BillId == billId)))
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

    public Task<PurchaseOrderBillLine?> FindForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<PurchaseOrderBillLine>(r => r.Id == id)).FirstOrDefaultAsync(ct);

    public Task<Guid?> FindOrderIdOfBillAsync(Guid billId, CancellationToken ct)
        => Query(new Specification<PurchaseOrderBillLine>(r => r.BillId == billId)).Select(r => (Guid?)r.OrderId).FirstOrDefaultAsync(ct);
}
