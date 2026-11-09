using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class RecordOperationHoursHandler(IWorkOrderRepository orders) : ICommandHandler<RecordOperationHoursCommand, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(RecordOperationHoursCommand request, CancellationToken cancellationToken)
    {
        // 1. Hours go on an operation of an order in progress.
        var wo = await orders.GetWithDetailsForUpdateAsync(request.WorkOrderId, cancellationToken);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Hours can only be recorded while the work order is in progress.");
        var op = wo.Operations.FirstOrDefault(o => o.Id == request.OperationId) ?? throw new NotFoundException($"Operation {request.OperationId} is not on this work order.");

        // 2. Record them; the first hours start a pending operation.
        op.ActualHours = request.ActualHours;
        op.Status = request.Status ?? (request.ActualHours > 0 && op.Status == OperationStatus.Pending ? OperationStatus.InProgress : op.Status);
        await orders.SaveChangesAsync(cancellationToken);
        return WorkOrderDto.From(wo);
    }
}
