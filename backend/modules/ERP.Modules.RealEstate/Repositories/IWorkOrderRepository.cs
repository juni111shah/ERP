using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for work orders. Implemented in WorkOrderRepository.cs.</summary>
public interface IWorkOrderRepository : IRepository<WorkOrder>
{
    /// <summary>The latest 100 work orders raised on the leases, newest first.</summary>
    Task<List<WorkOrder>> ListLatestOfLeasesAsync(List<Guid> leaseIds, CancellationToken ct);

    /// <summary>The latest 200 work orders assigned to the vendor, newest first.</summary>
    Task<List<WorkOrder>> ListLatestOfVendorAsync(Guid vendorPartyId, CancellationToken ct);

    /// <summary>The work order when it is assigned to the vendor, tracked for changes; null otherwise.</summary>
    Task<WorkOrder?> FindOfVendorForUpdateAsync(Guid id, Guid vendorPartyId, CancellationToken ct);

    /// <summary>The actual cost of the work completed or closed from <paramref name="from"/> to <paramref name="to"/>, of one legal entity or (null) all, added up per property.</summary>
    Task<Dictionary<Guid, decimal>> ActualCostsByPropertyAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct);

    /// <summary>The work orders reported from <paramref name="from"/> to <paramref name="to"/>, of one legal entity or (null) all, with their costs.</summary>
    Task<List<WorkOrderCostRow>> ListCostsReportedAsync(DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct);
}

/// <summary>A work order's property, category, status and costs.</summary>
public sealed record WorkOrderCostRow(Guid PropertyId, string? Category, WorkOrderStatus Status, decimal EstimatedCost, decimal? ActualCost);
