using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class UpdateWorkOrderHandler(IRepository<WorkOrder> workOrders, WorkOrderInputRules inputs, ICustomFieldService fields) : ICommandHandler<UpdateWorkOrderCommand, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(UpdateWorkOrderCommand cmd, CancellationToken ct)
    {
        // 1. Only an open work order can be edited, and it stays in its property.
        var workOrder = await workOrders.GetForUpdateAsync(cmd.Id, ct);
        if (!workOrder.IsOpen) throw new ConflictException($"A {workOrder.Status} work order can no longer be edited.");
        if (cmd.Input.PropertyId != workOrder.PropertyId) throw new FieldValidationException("propertyId", "A work order cannot move to another property.");

        // 2. Apply the form and the custom fields, save.
        await inputs.MapAsync(cmd.Input, workOrder, ct);
        await fields.ApplyAsync(Docs.WorkOrder, workOrder, cmd.Input.CustomFields, isCreate: false, ct);
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }
}
