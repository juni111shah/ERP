using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class TerminateLeaseHandler(
    IRepository<Lease> leases, IRentInstalmentRepository instalments, IRepository<Unit> units, IReadRepository<WorkOrder> workOrders, IUnitOfWork unitOfWork, LeaseDtoFactory factory)
    : ICommandHandler<TerminateLeaseCommand, TerminationResultDto>
{
    private DateOnly Today => factory.Today;

    /// <summary>
    /// Spec RE-LSE-10: cancels instalments that start after the date, pro-rates the instalment the date falls in, lists invoiced
    /// instalments that need a credit note (credit notes arrive with Finance), and vacates the unit.
    /// </summary>
    public Task<TerminationResultDto> Handle(TerminateLeaseCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Terminate the lease.
        var lease = await leases.GetForUpdateAsync(cmd.Id, token);
        lease.Terminate(cmd.Date, cmd.Reason);

        // 2. Cut the schedule at the date.
        var schedule = await instalments.ListNotCancelledOfLeaseForUpdateAsync(lease.Id, token);
        var cancelled = 0;
        var needCredit = new List<RentInstalment>();
        foreach (var i in schedule.Where(i => i.PeriodTo > cmd.Date))
        {
            if (i.Status == InstalmentStatus.Invoiced) { needCredit.Add(i); continue; }
            if (i.PeriodFrom > cmd.Date) { i.Status = InstalmentStatus.Cancelled; cancelled++; continue; }
            // The date falls inside this period: bill only the days used.
            var periodDays = i.PeriodTo.DayNumber - i.PeriodFrom.DayNumber + 1;
            var usedDays = cmd.Date.DayNumber - i.PeriodFrom.DayNumber + 1;
            i.Amount = Math.Round(i.Amount * usedDays / periodDays, 2, MidpointRounding.AwayFromZero);
            i.PeriodTo = cmd.Date;
        }

        // 3. Vacate the unit (repairs pending when it has open work orders).
        var unit = await units.GetForUpdateAsync(lease.UnitId, token);
        var repairsPending = await workOrders.AnyAsync(w => w.UnitId == unit.Id
            && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), token);
        if (unit.Status == UnitStatus.Occupied) unit.Vacate(Today, repairsPending);

        // 4. Save.
        await leases.SaveChangesAsync(token);
        return new TerminationResultDto(await factory.BuildAsync(lease, token), cancelled, needCredit.Select(InstalmentDto.From).ToList());
    }, ct);
}
