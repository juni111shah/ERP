using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Repositories;

internal sealed class WorkOrderRepository(IRepository<WorkOrder> inner) : RepositoryBase<WorkOrder>(inner), IWorkOrderRepository
{
    public async Task<WorkOrder> GetWithDetailsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<WorkOrder>(w => w.Id == id))
            .Include(w => w.Operations)
            .Include(w => w.MaterialIssues)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Work Order {id} not found.");

    public Task<WorkOrder?> FindWithDetailsAsync(Guid id, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w => w.Id == id))
            .Include(w => w.Operations)
            .Include(w => w.MaterialIssues)
            .FirstOrDefaultAsync(ct);

    public Task<List<WorkOrder>> ListOpenWithIssuesAsync(Guid legalEntityId, Guid? warehouseId, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w => w.LegalEntityId == legalEntityId
                && (w.Status == WorkOrderStatus.Draft || w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)
                && (warehouseId == null || w.WarehouseId == warehouseId)))
            .Include(w => w.MaterialIssues).ToListAsync(ct);

    public Task<List<WorkOrder>> ListCompletedForItemAsync(Guid legalEntityId, Guid itemId, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w =>
            w.LegalEntityId == legalEntityId &&
            w.ProducedItemId == itemId &&
            w.Status == WorkOrderStatus.Completed &&
            w.CompletedQuantity > 0))
            .ToListAsync(ct);

    public Task<int> CountOpenAsync(CancellationToken ct)
        => Query(new Specification<WorkOrder>(w =>
            w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress)).CountAsync(ct);

    public Task<List<WorkOrder>> ListCompletedSinceAsync(DateOnly from, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w =>
            w.Status == WorkOrderStatus.Completed && w.CompletionDate >= from)).ToListAsync(ct);
}
