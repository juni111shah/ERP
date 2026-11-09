using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class ChangeWorkOrderStatusHandler(IRepository<WorkOrder> workOrders, IRepository<Unit> units, ICustomFieldService fields, IUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<ChangeWorkOrderStatusCommand, WorkOrderDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public Task<WorkOrderDto> Handle(ChangeWorkOrderStatusCommand cmd, CancellationToken ct) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. Move the work order.
        var workOrder = await workOrders.GetForUpdateAsync(cmd.Id, token);
        if (cmd.ActualCost is { } actual) workOrder.ActualCost = actual;
        workOrder.MoveTo(cmd.Status, Today, cmd.ResolutionNotes);

        // 2. Spec 4.1: a unit parked as Unavailable for repairs returns to Vacant when its last open work order is done.
        if (!workOrder.IsOpen && workOrder.UnitId is { } unitId)
        {
            var unit = await units.GetForUpdateAsync(unitId, token);
            var othersOpen = await workOrders.AnyAsync(w => w.UnitId == unitId && w.Id != workOrder.Id
                && (w.Status == WorkOrderStatus.New || w.Status == WorkOrderStatus.Assigned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold), token);
            if (!othersOpen && unit.Status == UnitStatus.Unavailable && unit.UnavailableReason?.StartsWith("Repairs pending", StringComparison.Ordinal) == true)
                unit.MakeAvailable(Today);
        }

        // 3. Save.
        await workOrders.SaveChangesAsync(token);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, token));
    }, ct);
}
