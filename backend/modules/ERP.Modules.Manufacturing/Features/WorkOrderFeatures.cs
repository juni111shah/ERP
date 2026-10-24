using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Manufacturing.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Manufacturing.Features;

public record WorkOrderOperationDto(
    Guid Id,
    Guid WorkOrderId,
    int SequenceOrder,
    string OperationName,
    string WorkCenterName,
    decimal PlannedHours,
    decimal ActualHours,
    decimal HourlyRate,
    OperationStatus Status);

public record WorkOrderOperationInput(
    int SequenceOrder,
    string OperationName,
    string WorkCenterName,
    decimal PlannedHours,
    decimal HourlyRate);

public record WorkOrderDto(
    Guid Id,
    Guid LegalEntityId,
    string WorkOrderNumber,
    Guid BomId,
    string BomCode,
    Guid ProducedItemId,
    string ProducedItemCode,
    string ProducedItemName,
    decimal PlannedQuantity,
    decimal CompletedQuantity,
    decimal ScrappedQuantity,
    string Uom,
    Guid WarehouseId,
    DateOnly StartDate,
    DateOnly DueDate,
    DateOnly? CompletionDate,
    WorkOrderStatus Status,
    decimal EstimatedTotalCost,
    decimal ActualTotalCost,
    string? Notes,
    List<WorkOrderOperationDto> Operations,
    string? CustomFields,
    DateTimeOffset CreatedAt);

public record WorkOrderSummaryDto(
    Guid Id,
    Guid LegalEntityId,
    string WorkOrderNumber,
    string BomCode,
    string ProducedItemCode,
    string ProducedItemName,
    decimal PlannedQuantity,
    decimal CompletedQuantity,
    string Uom,
    DateOnly DueDate,
    WorkOrderStatus Status,
    decimal EstimatedTotalCost,
    decimal ActualTotalCost,
    DateTimeOffset CreatedAt);

public record CreateWorkOrderCommand(
    Guid LegalEntityId,
    Guid BomId,
    decimal PlannedQuantity,
    Guid WarehouseId,
    DateOnly StartDate,
    DateOnly DueDate,
    string? Notes,
    List<WorkOrderOperationInput>? Operations,
    string? CustomFields) : ICommand<WorkOrderDto>;

public record ReleaseWorkOrderCommand(Guid Id) : ICommand<WorkOrderDto>;

public record CompleteWorkOrderCommand(
    Guid Id,
    decimal CompletedQuantity,
    decimal ScrappedQuantity,
    decimal ActualTotalCost,
    string? Notes) : ICommand<WorkOrderDto>;

public record CancelWorkOrderCommand(Guid Id, string? Reason) : ICommand<Unit>;

public record GetWorkOrderByIdQuery(Guid Id) : IQuery<WorkOrderDto?>;

public class CreateWorkOrderValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.BomId).NotEmpty();
        RuleFor(x => x.PlannedQuantity).GreaterThan(0);
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public class CompleteWorkOrderValidator : AbstractValidator<CompleteWorkOrderCommand>
{
    public CompleteWorkOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CompletedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualTotalCost).GreaterThanOrEqualTo(0);
    }
}

public class WorkOrderHandlers :
    IRequestHandler<CreateWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<ReleaseWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<CompleteWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<CancelWorkOrderCommand, Unit>,
    IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDto?>
{
    private readonly IRepository<WorkOrder> _orders;
    private readonly IRepository<BillOfMaterials> _boms;
    private readonly INumberingService _numbering;

    public WorkOrderHandlers(
        IRepository<WorkOrder> orders,
        IRepository<BillOfMaterials> boms,
        INumberingService numbering)
    {
        _orders = orders;
        _boms = boms;
        _numbering = numbering;
    }

    public async Task<WorkOrderDto> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.BomId))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"BOM {request.BomId} not found.");

        var number = await _numbering.NextAsync("WorkOrder", request.LegalEntityId, request.StartDate, cancellationToken);

        // Estimate cost based on BOM unit cost * planned quantity
        var totalMaterial = bom.Components.Sum(c => c.QuantityRequired * c.UnitCost * (1 + (c.ScrapPercentage / 100m)));
        var unitCost = bom.OutputQuantity > 0 ? (totalMaterial / bom.OutputQuantity) + bom.LaborCostPerUnit + bom.OverheadCostPerUnit : 0;
        var estimatedCost = Math.Round(unitCost * request.PlannedQuantity, 2);

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
            Status = WorkOrderStatus.Draft,
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

        _orders.Add(wo);
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public async Task<WorkOrderDto> Handle(ReleaseWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Query(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Work Order {request.Id} not found.");

        wo.Status = WorkOrderStatus.InProgress;
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public async Task<WorkOrderDto> Handle(CompleteWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Query(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Work Order {request.Id} not found.");

        wo.Status = WorkOrderStatus.Completed;
        wo.CompletedQuantity = request.CompletedQuantity;
        wo.ScrappedQuantity = request.ScrappedQuantity;
        wo.ActualTotalCost = request.ActualTotalCost;
        wo.CompletionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            wo.Notes = (wo.Notes + " | Completed: " + request.Notes).Trim();
        }

        foreach (var op in wo.Operations)
        {
            op.Status = OperationStatus.Completed;
        }

        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public async Task<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.GetForUpdateAsync(request.Id, cancellationToken);
        wo.Status = WorkOrderStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            wo.Notes = (wo.Notes + " | Cancelled: " + request.Reason).Trim();
        }
        await _orders.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    public async Task<WorkOrderDto?> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Query(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .FirstOrDefaultAsync(cancellationToken);

        return wo is null ? null : Map(wo);
    }

    private static WorkOrderDto Map(WorkOrder w) => new(
        w.Id,
        w.LegalEntityId,
        w.WorkOrderNumber,
        w.BomId,
        w.BomCode,
        w.ProducedItemId,
        w.ProducedItemCode,
        w.ProducedItemName,
        w.PlannedQuantity,
        w.CompletedQuantity,
        w.ScrappedQuantity,
        w.Uom,
        w.WarehouseId,
        w.StartDate,
        w.DueDate,
        w.CompletionDate,
        w.Status,
        w.EstimatedTotalCost,
        w.ActualTotalCost,
        w.Notes,
        w.Operations.OrderBy(o => o.SequenceOrder).Select(o => new WorkOrderOperationDto(
            o.Id,
            o.WorkOrderId,
            o.SequenceOrder,
            o.OperationName,
            o.WorkCenterName,
            o.PlannedHours,
            o.ActualHours,
            o.HourlyRate,
            o.Status)).ToList(),
        w.CustomFields,
        w.CreatedAt);
}
