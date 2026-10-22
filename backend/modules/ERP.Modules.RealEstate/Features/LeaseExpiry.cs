using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Ends (or holds over) leases past their end date, frees their units and releases lapsed reservations. Idempotent; used by the manual command and the nightly job.</summary>
internal sealed class LeaseExpiryProcessor(IRepository<Lease> leases, IRepository<Unit> units, IUnitOfWork unitOfWork)
{
    public Task<ExpiryRunResultDto> RunAsync(DateOnly today, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        int ended = 0, heldOver = 0, released = 0;
        var expired = await leases.Tracked(new Specification<Lease>(l => l.Status == LeaseStatus.Active && l.EndDate < today)).ToListAsync(token);
        foreach (var lease in expired)
        {
            if (lease.Expire(today) == LeaseStatus.HoldingOver) { heldOver++; continue; }
            var unit = await units.GetForUpdateAsync(lease.UnitId, token);
            if (unit.Status == UnitStatus.Occupied) unit.Vacate(today);
            ended++;
        }
        var lapsed = await units.Tracked(new Specification<Unit>(u => u.Status == UnitStatus.Reserved && u.ReservedUntil != null && u.ReservedUntil < today)).ToListAsync(token);
        foreach (var unit in lapsed) { unit.ReleaseReservation(today); released++; }
        await leases.SaveChangesAsync(token);
        return new ExpiryRunResultDto(ended, heldOver, released);
    }, ct);
}

/// <summary>The nightly housekeeping for a real estate workspace.</summary>
internal sealed class LeaseExpiryJob(LeaseExpiryProcessor processor) : IScheduledJob
{
    public string Name => "realestate.lease-expiry";
    public string? ModuleKey => RealEstateModule.Key;
    public Task RunAsync(DateOnly today, CancellationToken ct) => processor.RunAsync(today, ct);
}
