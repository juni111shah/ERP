using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class CreateWorkOrderHandler(
    IRepository<WorkOrder> workOrders, IReadRepository<Property> properties, WorkOrderInputRules inputs, INumberingService numbers, ICustomFieldService fields, IClock clock)
    : ICommandHandler<CreateWorkOrderCommand, WorkOrderDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<WorkOrderDto> Handle(CreateWorkOrderCommand cmd, CancellationToken ct)
    {
        // 1. The property must exist; the work order takes its legal entity.
        var i = cmd.Input;
        var property = await properties.FindAsync(i.PropertyId, ct) ?? throw new FieldValidationException("propertyId", "Property not found.");
        var workOrder = new WorkOrder { LegalEntityId = property.LegalEntityId, ReportedOn = i.ReportedOn ?? Today };

        // 2. Apply the form, number it, apply the custom fields, save.
        await inputs.MapAsync(i, workOrder, ct);
        workOrder.Number = await numbers.NextAsync(Docs.WorkOrder, workOrder.LegalEntityId, DateOnly.FromDateTime(clock.UtcNow), ct);
        await fields.ApplyAsync(Docs.WorkOrder, workOrder, i.CustomFields, isCreate: true, ct);
        workOrders.Add(workOrder);
        await workOrders.SaveChangesAsync(ct);
        return WorkOrderDto.From(workOrder, await fields.ReadAsync(Docs.WorkOrder, workOrder, ct));
    }
}
