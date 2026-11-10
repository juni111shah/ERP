using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class WorkOrderRepository(IRepository<WorkOrder> inner) : RepositoryBase<WorkOrder>(inner), IWorkOrderRepository
{
    public Task<List<WorkOrder>> ListLatestOfLeasesAsync(List<Guid> leaseIds, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w => w.LeaseId != null && leaseIds.Contains(w.LeaseId.Value))).OrderByDescending(w => w.ReportedOn).Take(100).ToListAsync(ct);

    public Task<List<WorkOrder>> ListLatestOfVendorAsync(Guid vendorPartyId, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w => w.VendorPartyId == vendorPartyId)).OrderByDescending(w => w.ReportedOn).Take(200).ToListAsync(ct);

    public Task<WorkOrder?> FindOfVendorForUpdateAsync(Guid id, Guid vendorPartyId, CancellationToken ct)
        => Tracked(new Specification<WorkOrder>(x => x.Id == id && x.VendorPartyId == vendorPartyId)).FirstOrDefaultAsync(ct);

    public async Task<Dictionary<Guid, decimal>> ActualCostsByPropertyAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => (await Query(new Specification<WorkOrder>(w => (w.Status == WorkOrderStatus.Completed || w.Status == WorkOrderStatus.Closed) && w.ActualCost != null && w.CompletedOn >= from && w.CompletedOn <= to && (legalEntityId == null || w.LegalEntityId == legalEntityId)))
            .Select(w => new { w.PropertyId, w.ActualCost }).ToListAsync(ct)).GroupBy(w => w.PropertyId).ToDictionary(g => g.Key, g => g.Sum(w => w.ActualCost ?? 0));

    public Task<List<WorkOrderCostRow>> ListCostsReportedAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<WorkOrder>(w => w.ReportedOn >= from && w.ReportedOn <= to && (legalEntityId == null || w.LegalEntityId == legalEntityId)))
            .Select(w => new WorkOrderCostRow(w.PropertyId, w.Category, w.Status, w.EstimatedCost, w.ActualCost)).ToListAsync(ct);
}
