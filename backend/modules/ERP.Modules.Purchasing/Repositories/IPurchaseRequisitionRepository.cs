using ERP.Application.Abstractions;
using ERP.Modules.Purchasing.Domain;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>Data access for purchase requisitions. Implemented in PurchaseRequisitionRepository.cs.</summary>
public interface IPurchaseRequisitionRepository : IRepository<PurchaseRequisition>
{
    /// <summary>The requisition with its lines, tracked for changes; null when not found.</summary>
    Task<PurchaseRequisition?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The requisition, tracked for changes, while it is still the one converted to <paramref name="orderId"/>; null otherwise.</summary>
    Task<PurchaseRequisition?> FindConvertedToOrderForUpdateAsync(Guid id, Guid orderId, CancellationToken ct);
}
