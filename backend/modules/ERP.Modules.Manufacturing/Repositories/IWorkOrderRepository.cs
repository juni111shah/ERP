using ERP.Application.Abstractions;
using ERP.Modules.Manufacturing.Domain;

namespace ERP.Modules.Manufacturing.Repositories;

/// <summary>Data access for production work orders. Implemented in WorkOrderRepository.cs.</summary>
public interface IWorkOrderRepository : IRepository<WorkOrder>
{
    /// <summary>The work order with its operations and material issues, tracked for changes. Throws NotFoundException.</summary>
    Task<WorkOrder> GetWithDetailsForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The work order with its operations and material issues, read-only; null when it does not exist.</summary>
    Task<WorkOrder?> FindWithDetailsAsync(Guid id, CancellationToken ct);

    /// <summary>The legal entity's draft, planned and in-progress work orders (of one warehouse when given), with their material issues.</summary>
    Task<List<WorkOrder>> ListOpenWithIssuesAsync(Guid legalEntityId, Guid? warehouseId, CancellationToken ct);

    /// <summary>The legal entity's completed work orders that made some of the item.</summary>
    Task<List<WorkOrder>> ListCompletedForItemAsync(Guid legalEntityId, Guid itemId, CancellationToken ct);

    /// <summary>The number of planned and in-progress work orders.</summary>
    Task<int> CountOpenAsync(CancellationToken ct);

    /// <summary>The work orders completed on or after <paramref name="from"/>.</summary>
    Task<List<WorkOrder>> ListCompletedSinceAsync(DateOnly from, CancellationToken ct);
}
