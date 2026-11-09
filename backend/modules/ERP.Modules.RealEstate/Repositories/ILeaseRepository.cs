using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>Data access for leases. Implemented in LeaseRepository.cs.</summary>
public interface ILeaseRepository : IRepository<Lease>
{
    /// <summary>The active or held-over leases of the units.</summary>
    Task<List<Lease>> ListOccupyingOfUnitsAsync(List<Guid> unitIds, CancellationToken ct);

    /// <summary>The annual rent of the property's active and held-over leases, added up.</summary>
    Task<decimal> ContractedRentOfPropertyAsync(Guid propertyId, CancellationToken ct);

    /// <summary>The id of the lease's renewal that is not cancelled; null when there is none.</summary>
    Task<Guid?> FindRenewalIdAsync(Guid leaseId, CancellationToken ct);

    /// <summary>The number of another active or held-over lease of the unit overlapping the period (the lease being renewed does not count); null when there is none.</summary>
    Task<string?> FindOverlappingOccupyingNumberAsync(Guid leaseId, Guid unitId, DateOnly start, DateOnly end, Guid? renewalOfLeaseId, CancellationToken ct);

    /// <summary>Active leases that ended before <paramref name="today"/>, tracked for changes.</summary>
    Task<List<Lease>> ListExpiredActiveForUpdateAsync(DateOnly today, CancellationToken ct);

    /// <summary>The leases with the given ids.</summary>
    Task<List<Lease>> ListByIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The tenant's leases the portal shows (not drafts or cancelled ones), latest start first.</summary>
    Task<List<Lease>> ListOfTenantForPortalAsync(Guid tenantPartyId, CancellationToken ct);

    /// <summary>The ids of all the tenant's leases.</summary>
    Task<List<Guid>> ListIdsOfTenantAsync(Guid tenantPartyId, CancellationToken ct);

    /// <summary>The lease when it belongs to the tenant; null otherwise.</summary>
    Task<Lease?> FindOfTenantAsync(Guid leaseId, Guid tenantPartyId, CancellationToken ct);

    /// <summary>Active and held-over leases ending on or before <paramref name="horizon"/>, earliest end first.</summary>
    Task<List<Lease>> ListOccupyingEndingByAsync(DateOnly horizon, CancellationToken ct);

    /// <summary>The renewals (not cancelled) of the given leases.</summary>
    Task<List<LeaseRenewalLink>> ListLiveRenewalsOfAsync(List<Guid> leaseIds, CancellationToken ct);

    /// <summary>The active and held-over leases of one legal entity or (null) all, of the given properties or (empty) all, as the rent roll shows them.</summary>
    Task<List<RentRollLease>> ListRentRollLeasesAsync(Guid? legalEntityId, Guid[] propertyIds, CancellationToken ct);

    /// <summary>The property and legal entity of each of the leases, by id.</summary>
    Task<Dictionary<Guid, LeasePlacement>> MapPlacementsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The annual rent of the active and held-over leases of one legal entity or (null) all, added up per property.</summary>
    Task<Dictionary<Guid, decimal>> ContractedRentByPropertyAsync(Guid? legalEntityId, CancellationToken ct);

    /// <summary>The property of each of the leases, by id.</summary>
    Task<Dictionary<Guid, Guid>> MapPropertyIdsAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>End date and annual rent of the active leases of one legal entity or (null) all.</summary>
    Task<List<LeaseEndAndRent>> ListActiveEndsAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>A renewal lease, the lease it renews and its status.</summary>
public sealed record LeaseRenewalLink(Guid Id, Guid Of, LeaseStatus Status);

/// <summary>A lease as the rent roll shows it.</summary>
public sealed record RentRollLease(Guid Id, string? Number, Guid UnitId, Guid PropertyId, Guid LesseePartyId, DateOnly StartDate, DateOnly EndDate, decimal AnnualRent, Guid? TaxCodeId);

/// <summary>Where a lease belongs: its property and legal entity.</summary>
public sealed record LeasePlacement(Guid Id, Guid PropertyId, Guid LegalEntityId);

/// <summary>A lease's end date and annual rent.</summary>
public sealed record LeaseEndAndRent(DateOnly EndDate, decimal AnnualRent);
