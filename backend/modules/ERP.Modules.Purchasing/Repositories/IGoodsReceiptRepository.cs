using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>What one goods receipt line received against an order line, and whether stock moved.</summary>
public sealed record ReceivedOrderLine(Guid PurchaseOrderLineId, decimal QuantityReceived, bool NoStockMoved);

/// <summary>Data access for goods receipts. Implemented in GoodsReceiptRepository.cs.</summary>
public interface IGoodsReceiptRepository : IRepository<GoodsReceipt>
{
    /// <summary>The receipt with its lines, tracked for changes; null when not found.</summary>
    Task<GoodsReceipt?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Every receipt line of a purchase order.</summary>
    Task<List<ReceivedOrderLine>> ListReceivedLinesOfOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>The warehouse of the order's latest receipt that named one; null when none did.</summary>
    Task<Guid?> LatestWarehouseOfOrderAsync(Guid orderId, CancellationToken ct);
}
