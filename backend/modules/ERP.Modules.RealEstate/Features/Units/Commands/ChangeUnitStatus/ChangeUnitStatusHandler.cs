using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class ChangeUnitStatusHandler(IRepository<Unit> units, IReadRepository<WorkOrder> workOrders, ICustomFieldService fields, IClock clock)
    : ICommandHandler<ChangeUnitStatusCommand, UnitDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<UnitDto> Handle(ChangeUnitStatusCommand cmd, CancellationToken ct)
    {
        // 1. Load it and apply the action (a unit with open work orders cannot be made available).
        var unit = await units.GetForUpdateAsync(cmd.Id, ct);
        switch (cmd.Action)
        {
            case UnitAction.Reserve: unit.Reserve(cmd.Until); break;
            case UnitAction.ReleaseReservation: unit.ReleaseReservation(Today); break;
            case UnitAction.MakeUnavailable: unit.MakeUnavailable(cmd.Reason ?? ""); break;
            case UnitAction.MakeAvailable:
                if (await workOrders.AnyAsync(w => w.UnitId == unit.Id && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), ct))
                    throw new ConflictException("Close or cancel the open work orders of this unit first.");
                unit.MakeAvailable(Today); break;
            default: throw new FieldValidationException("action", "Unknown unit action.");
        }

        // 2. Save.
        await units.SaveChangesAsync(ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }
}
