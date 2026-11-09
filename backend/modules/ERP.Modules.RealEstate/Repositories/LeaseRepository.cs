using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class LeaseRepository(IRepository<Lease> inner) : RepositoryBase<Lease>(inner), ILeaseRepository
{
    public Task<List<Lease>> ListOccupyingOfUnitsAsync(List<Guid> unitIds, CancellationToken ct)
        => Query(new Specification<Lease>(l => unitIds.Contains(l.UnitId) && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver))).ToListAsync(ct);

    // At most one occupying lease per unit, so this list is bounded; summed here because SQLite cannot aggregate decimals.
    public async Task<decimal> ContractedRentOfPropertyAsync(Guid propertyId, CancellationToken ct)
        => (await Query(new Specification<Lease>(l => l.PropertyId == propertyId && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver)))
            .Select(l => l.AnnualRent).ToListAsync(ct)).Sum();

    public Task<Guid?> FindRenewalIdAsync(Guid leaseId, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.RenewalOfLeaseId == leaseId && l.Status != LeaseStatus.Cancelled)).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(ct);

    public Task<string?> FindOverlappingOccupyingNumberAsync(Guid leaseId, Guid unitId, DateOnly start, DateOnly end, Guid? renewalOfLeaseId, CancellationToken ct)
        => Query(new Specification<Lease>(l =>
                l.Id != leaseId && l.UnitId == unitId && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver)
                && l.StartDate <= end && l.EndDate >= start && l.Id != renewalOfLeaseId))
            .Select(l => l.Number).FirstOrDefaultAsync(ct);

    public Task<List<Lease>> ListExpiredActiveForUpdateAsync(DateOnly today, CancellationToken ct)
        => Tracked(new Specification<Lease>(l => l.Status == LeaseStatus.Active && l.EndDate < today)).ToListAsync(ct);

    public Task<List<Lease>> ListByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Lease>(l => ids.Contains(l.Id))).ToListAsync(ct);

    public Task<List<Lease>> ListOfTenantForPortalAsync(Guid tenantPartyId, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.LesseePartyId == tenantPartyId && l.Status != LeaseStatus.Draft && l.Status != LeaseStatus.Cancelled)).OrderByDescending(l => l.StartDate).ToListAsync(ct);

    public Task<List<Guid>> ListIdsOfTenantAsync(Guid tenantPartyId, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.LesseePartyId == tenantPartyId)).Select(l => l.Id).ToListAsync(ct);

    public Task<Lease?> FindOfTenantAsync(Guid leaseId, Guid tenantPartyId, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.Id == leaseId && l.LesseePartyId == tenantPartyId)).FirstOrDefaultAsync(ct);

    public Task<List<Lease>> ListOccupyingEndingByAsync(DateOnly horizon, CancellationToken ct)
        => Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && l.EndDate <= horizon)).OrderBy(l => l.EndDate).ToListAsync(ct);

    public Task<List<LeaseRenewalLink>> ListLiveRenewalsOfAsync(List<Guid> leaseIds, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.RenewalOfLeaseId != null && leaseIds.Contains(l.RenewalOfLeaseId.Value) && l.Status != LeaseStatus.Cancelled))
            .Select(l => new LeaseRenewalLink(l.Id, l.RenewalOfLeaseId!.Value, l.Status)).ToListAsync(ct);

    public Task<List<RentRollLease>> ListRentRollLeasesAsync(Guid? legalEntityId, Guid[] propertyIds, CancellationToken ct)
        => Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && (legalEntityId == null || l.LegalEntityId == legalEntityId) && (propertyIds.Length == 0 || propertyIds.Contains(l.PropertyId))))
            .Select(l => new RentRollLease(l.Id, l.Number, l.UnitId, l.PropertyId, l.LesseePartyId, l.StartDate, l.EndDate, l.AnnualRent, l.TaxCodeId)).ToListAsync(ct);

    public Task<Dictionary<Guid, LeasePlacement>> MapPlacementsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Lease>(l => ids.Contains(l.Id))).Select(l => new LeasePlacement(l.Id, l.PropertyId, l.LegalEntityId)).ToDictionaryAsync(l => l.Id, ct);

    public async Task<Dictionary<Guid, decimal>> ContractedRentByPropertyAsync(Guid? legalEntityId, CancellationToken ct)
        => (await Query(new Specification<Lease>(l => (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.HoldingOver) && (legalEntityId == null || l.LegalEntityId == legalEntityId))).Select(l => new { l.PropertyId, l.AnnualRent }).ToListAsync(ct))
            .GroupBy(l => l.PropertyId).ToDictionary(g => g.Key, g => g.Sum(l => l.AnnualRent));

    public Task<Dictionary<Guid, Guid>> MapPropertyIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Lease>(l => ids.Contains(l.Id))).Select(l => new { l.Id, l.PropertyId }).ToDictionaryAsync(l => l.Id, l => l.PropertyId, ct);

    public Task<List<LeaseEndAndRent>> ListActiveEndsAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Lease>(l => l.Status == LeaseStatus.Active && (legalEntityId == null || l.LegalEntityId == legalEntityId))).Select(l => new LeaseEndAndRent(l.EndDate, l.AnnualRent)).ToListAsync(ct);
}
