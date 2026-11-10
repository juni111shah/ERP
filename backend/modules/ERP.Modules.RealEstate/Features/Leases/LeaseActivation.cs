using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Microsoft.EntityFrameworkCore;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

/// <summary>Activates a draft lease (shared by activate lease and create lease with ActivateNow).</summary>
internal sealed class LeaseActivation(
    ILeaseRepository leases, IRepository<RentInstalment> instalments, IRepository<Unit> units, IUnitOfWork unitOfWork, ICurrentUser user, IClock clock, LeaseDtoFactory factory)
{
    /// <summary>Spec RE-LSE-03: no overlapping active lease on the unit; unit -> Occupied (or the renewed lease -> Renewed); schedule fixed.</summary>
    public Task<LeaseDto> ActivateAsync(Guid id, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        var lease = await leases.GetForUpdateAsync(id, token);
        if (lease.Status != LeaseStatus.Draft) throw new ConflictException($"Only a draft lease can be activated (this lease is {lease.Status}).");
        var unit = await units.GetForUpdateAsync(lease.UnitId, token);

        var overlapping = await leases.FindOverlappingOccupyingNumberAsync(lease.Id, lease.UnitId, lease.StartDate, lease.EndDate, lease.RenewalOfLeaseId, token);
        if (overlapping is not null)
            throw new ConflictException($"Unit {unit.UnitNumber} already has an active lease ({overlapping}) for that period.");

        try
        {
            if (lease.RenewalOfLeaseId is { } oldId)
            {
                // The old lease must leave the "occupying" set first: only one occupying lease per unit may exist at any time.
                var old = await leases.GetForUpdateAsync(oldId, token);
                var stillOccupying = old.IsOccupying;
                old.MarkRenewed();
                await leases.SaveChangesAsync(token);
                // the nightly job ended the old lease and freed the unit before the renewal was activated
                if (!stillOccupying) unit.Occupy();
            }
            else unit.Occupy();

            lease.Activate(clock.UtcNow, user.UserId);
            instalments.AddRange(RentSchedule.Generate(lease.StartDate, lease.EndDate, lease.AnnualRent, lease.PaymentFrequency, lease.GraceDays)
                .Select(s => new RentInstalment
                {
                    LeaseId = lease.Id, LegalEntityId = lease.LegalEntityId, Sequence = s.Sequence, PeriodFrom = s.PeriodFrom, PeriodTo = s.PeriodTo,
                    DueDate = s.DueDate, Amount = s.Amount,
                }));
            await leases.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException($"Unit {unit.UnitNumber} was just let by someone else.");
        }
        return await factory.BuildAsync(lease, token);
    }, ct);
}
