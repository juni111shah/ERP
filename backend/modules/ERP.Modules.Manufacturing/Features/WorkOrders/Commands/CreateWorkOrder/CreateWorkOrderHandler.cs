using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Organizations;
using ERP.Modules.Manufacturing.Domain;
using ERP.Modules.Manufacturing.Repositories;

namespace ERP.Modules.Manufacturing.Features;

internal sealed class CreateWorkOrderHandler(
    IWorkOrderRepository orders,
    IBillOfMaterialsRepository boms,
    INumberingService numbering,
    IStockPostingService stockPosting,
    ILegalEntityGuard guard)
    : ICommandHandler<CreateWorkOrderCommand, WorkOrderDto>
{
    public async Task<WorkOrderDto> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. The BOM must be active and of this legal entity, the warehouse usable by it.
        var bom = await boms.GetWithComponentsAsync(request.BomId, cancellationToken);

        await guard.RequireAsync(request.LegalEntityId, cancellationToken);
        if (bom.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("bomId", "This BOM belongs to another legal entity.");
        if (!bom.IsActive) throw new ConflictException($"BOM {bom.Code} is inactive.");
        if (!await stockPosting.IsWarehouseUsableAsync(request.WarehouseId, request.LegalEntityId, cancellationToken))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");

        // 2. Number it.
        var number = await numbering.NextAsync("WorkOrder", request.LegalEntityId, request.StartDate, cancellationToken);

        // 3. Estimate at today's component costs in the warehouse the order will draw from.
        var costs = await BomStructure.CurrentCostsAsync(bom.Components, stockPosting, request.WarehouseId, cancellationToken);
        var totalMaterial = BomStructure.MaterialCost(bom.Components, costs);
        var unitCost = bom.OutputQuantity > 0 ? (totalMaterial / bom.OutputQuantity) + bom.LaborCostPerUnit + bom.OverheadCostPerUnit : 0;
        var estimatedCost = Math.Round(unitCost * request.PlannedQuantity, 2);

        // 4. Build the order with its operations.
        var wo = new WorkOrder
        {
            LegalEntityId = request.LegalEntityId,
            WorkOrderNumber = number,
            BomId = bom.Id,
            BomCode = bom.Code,
            ProducedItemId = bom.ProducedItemId,
            ProducedItemCode = bom.ProducedItemCode,
            ProducedItemName = bom.ProducedItemName,
            PlannedQuantity = request.PlannedQuantity,
            CompletedQuantity = 0,
            ScrappedQuantity = 0,
            Uom = bom.Uom,
            WarehouseId = request.WarehouseId,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            Status = WorkOrderStatus.Planned,
            EstimatedTotalCost = estimatedCost,
            ActualTotalCost = 0,
            Notes = request.Notes?.Trim(),
            CustomFields = request.CustomFields
        };

        if (request.Operations != null)
        {
            foreach (var op in request.Operations)
            {
                wo.Operations.Add(new WorkOrderOperation
                {
                    LegalEntityId = wo.LegalEntityId,
                    WorkOrderId = wo.Id,
                    SequenceOrder = op.SequenceOrder,
                    OperationName = op.OperationName.Trim(),
                    WorkCenterName = op.WorkCenterName.Trim(),
                    PlannedHours = op.PlannedHours,
                    ActualHours = 0,
                    HourlyRate = op.HourlyRate,
                    Status = OperationStatus.Pending
                });
            }
        }

        // 5. Save.
        orders.Add(wo);
        await orders.SaveChangesAsync(cancellationToken);
        return WorkOrderDto.From(wo);
    }
}
