using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;
using MediatR;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class CancelWorkOrderHandler(IWorkOrderRepository orders, WorkOrderMaterials materials, IUnitOfWork unitOfWork) : ICommandHandler<CancelWorkOrderCommand>
{
    public Task<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken) => unitOfWork.InTransactionAsync(async token =>
    {
        // 1. An order that has made something cannot be cancelled.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Cancelled, isSystem: false);
        if (wo.CompletedQuantity > 0)
            throw new ConflictException("Cannot cancel a Work Order with completed quantity.");

        // 2. Put the material back and reverse the journals.
        var memo = $"Cancelled WO {wo.WorkOrderNumber}";
        await materials.UndoMaterialAsync(wo, memo, token);
        await materials.ReverseJournalsAsync(wo, memo, token);

        // 3. Cancel and save.
        wo.Status = WorkOrderStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            wo.Notes = (wo.Notes + " | Cancelled: " + request.Reason).Trim();
        }
        await orders.SaveChangesAsync(token);
        return Unit.Value;
    }, cancellationToken);
}
