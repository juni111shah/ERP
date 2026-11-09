using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class UnitRepository(IRepository<Unit> inner) : RepositoryBase<Unit>(inner), IUnitRepository
{
    public Task<List<string>> ListExistingNumbersAsync(Guid propertyId, List<string> numbers, CancellationToken ct)
        => Query(new Specification<Unit>(u => u.PropertyId == propertyId && numbers.Contains(u.UnitNumber))).Select(u => u.UnitNumber).ToListAsync(ct);

    public Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Unit>(u => ids.Contains(u.Id))).Select(u => new { u.Id, u.UnitNumber }).ToDictionaryAsync(u => u.Id, u => u.UnitNumber, ct);

    public Task<List<Unit>> ListLapsedReservationsForUpdateAsync(DateOnly today, CancellationToken ct)
        => Tracked(new Specification<Unit>(u => u.Status == UnitStatus.Reserved && u.ReservedUntil != null && u.ReservedUntil < today)).ToListAsync(ct);

    public Task<Dictionary<Guid, UnitNumberAndRent>> MapNumberAndAskingRentAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Unit>(u => ids.Contains(u.Id))).Select(u => new UnitNumberAndRent(u.Id, u.UnitNumber, u.AskingRent)).ToDictionaryAsync(u => u.Id, ct);

    public Task<Dictionary<Guid, RentRollUnit>> MapRentRollUnitsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Unit>(u => ids.Contains(u.Id))).Select(u => new RentRollUnit(u.Id, u.UnitNumber, u.AreaSqft, u.UnitTypeId, u.Usage)).ToDictionaryAsync(u => u.Id, ct);

    public Task<List<UnitOccupancyRow>> ListOccupancyRowsAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Unit>(u => legalEntityId == null || u.LegalEntityId == legalEntityId)).Select(u => new UnitOccupancyRow(u.Id, u.PropertyId, u.Status, u.AskingRent)).ToListAsync(ct);

    public Task<List<UnitStatusRow>> ListStatusesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Unit>(u => legalEntityId == null || u.LegalEntityId == legalEntityId)).Select(u => new UnitStatusRow(u.PropertyId, u.Status)).ToListAsync(ct);
}
