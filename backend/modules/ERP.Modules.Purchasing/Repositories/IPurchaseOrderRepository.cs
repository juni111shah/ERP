using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>Data access for purchase orders. Implemented in PurchaseOrderRepository.cs.</summary>
public interface IPurchaseOrderRepository : IRepository<PurchaseOrder>
{
    /// <summary>The order with its lines, tracked for changes; null when not found.</summary>
    Task<PurchaseOrder?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The total of every order that is not cancelled.</summary>
    Task<decimal> TotalNotCancelledAsync(CancellationToken ct);

    /// <summary>How many orders are open (draft, submitted, approved or partly received).</summary>
    Task<int> CountOpenAsync(CancellationToken ct);

    /// <summary>The total of the open orders (draft, submitted, approved or partly received).</summary>
    Task<decimal> TotalOpenAsync(CancellationToken ct);
}
