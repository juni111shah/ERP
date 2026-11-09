using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>Data access for the links between order lines and the bills raised from them. Implemented in PurchaseOrderBillLineRepository.cs.</summary>
public interface IPurchaseOrderBillLineRepository : IRepository<PurchaseOrderBillLine>
{
    /// <summary>The bills raised from an order, each once, in the order they were raised.</summary>
    Task<List<Guid>> ListBillIdsOfOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>The bills raised from an order, each once, in no particular order.</summary>
    Task<List<Guid>> ListDistinctBillIdsOfOrderAsync(Guid orderId, CancellationToken ct);

    /// <summary>The bill links of an order line (only those of <paramref name="billId"/> when it is set), newest first, tracked for changes.</summary>
    Task<List<PurchaseOrderBillLine>> ListForOrderLineForUpdateAsync(Guid orderLineId, Guid? billId, CancellationToken ct);

    /// <summary>The link, tracked for changes; null when not found.</summary>
    Task<PurchaseOrderBillLine?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The order a bill was raised from; null when it was not raised from one.</summary>
    Task<Guid?> FindOrderIdOfBillAsync(Guid billId, CancellationToken ct);
}
