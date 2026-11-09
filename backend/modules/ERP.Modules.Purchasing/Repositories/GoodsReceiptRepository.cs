using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Repositories;

internal sealed class GoodsReceiptRepository(IRepository<GoodsReceipt> inner) : RepositoryBase<GoodsReceipt>(inner), IGoodsReceiptRepository
{
    public Task<GoodsReceipt?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => Tracked(new Specification<GoodsReceipt>(x => x.Id == id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

    public Task<List<ReceivedOrderLine>> ListReceivedLinesOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<GoodsReceipt>(g => g.PurchaseOrderId == orderId)).SelectMany(g => g.Lines)
            .Select(l => new ReceivedOrderLine(l.PurchaseOrderLineId, l.QuantityReceived, l.NoStockMoved)).ToListAsync(ct);

    public Task<Guid?> LatestWarehouseOfOrderAsync(Guid orderId, CancellationToken ct)
        => Query(new Specification<GoodsReceipt>(g => g.PurchaseOrderId == orderId && g.WarehouseId != null))
            .OrderByDescending(g => g.ReceiptDate).Select(g => g.WarehouseId).FirstOrDefaultAsync(ct);
}
