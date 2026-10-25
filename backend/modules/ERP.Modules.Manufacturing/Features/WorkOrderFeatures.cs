using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
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
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingService _posting;
    private readonly IReadRepository<Item> _items;
    private readonly IUnitOfWork _unitOfWork;

    public WorkOrderHandlers(
        IRepository<WorkOrder> orders,
        IRepository<BillOfMaterials> boms,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IPostingService posting,
        IReadRepository<Item> items,
        IUnitOfWork unitOfWork)
    {
        _orders = orders;
        _boms = boms;
        _numbering = numbering;
        _stockPosting = stockPosting;
        _posting = posting;
        _items = items;
        _unitOfWork = unitOfWork;
    }

    public async Task<WorkOrderDto> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == request.BomId))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"BOM {request.BomId} not found.");

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

        _orders.Add(wo);
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public async Task<WorkOrderDto> Handle(ReleaseWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Tracked(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Work Order {request.Id} not found.");

        if (wo.Status != WorkOrderStatus.Planned)
            throw new ConflictException($"Work Order must be Planned before releasing (currently {wo.Status}).");

        wo.Status = WorkOrderStatus.InProgress;
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public async Task<WorkOrderDto> Handle(CompleteWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Tracked(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Work Order {request.Id} not found.");

        if (wo.Status != WorkOrderStatus.InProgress)
            throw new ConflictException($"Work Order must be in progress before completing (currently {wo.Status}).");

        var bom = await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == wo.BomId))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"BOM {wo.BomId} not found.");

        // Reject completion if a BOM component item is missing or not a Product
        foreach (var c in bom.Components)
        {
            var compItem = await _items.FindAsync(c.ComponentItemId, cancellationToken);
            if (compItem is null || compItem.Type != ItemType.Product)
                throw new ConflictException($"Component item '{c.ComponentItemCode}' was not found or is not a Product.");
        }

        return await _unitOfWork.InTransactionAsync(async token =>
        {
            var completionDate = wo.CompletionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            wo.CompletionDate = completionDate;
            wo.Status = WorkOrderStatus.Completed;
            wo.CompletedQuantity = request.CompletedQuantity;
            wo.ScrappedQuantity = request.ScrappedQuantity;
            if (!string.IsNullOrWhiteSpace(request.Notes))
            {
                wo.Notes = (wo.Notes + " | Completed: " + request.Notes).Trim();
            }

            foreach (var op in wo.Operations)
            {
                op.Status = OperationStatus.Completed;
            }

            // Compute conversion cost from operations on the work order or BOM unit costs
            var opCost = wo.Operations.Sum(o => (o.ActualHours > 0 ? o.ActualHours : o.PlannedHours) * o.HourlyRate);
            var bomConversionPerUnit = bom.LaborCostPerUnit + bom.OverheadCostPerUnit;
            var conversionCost = opCost > 0 ? opCost : Math.Round(bomConversionPerUnit * wo.CompletedQuantity, 2);

            decimal totalMaterialCost = 0m;
            if (wo.CompletedQuantity > 0 && bom.Components.Count > 0)
            {
                var factor = wo.CompletedQuantity / (bom.OutputQuantity > 0 ? bom.OutputQuantity : 1);
                foreach (var c in bom.Components)
                {
                    if (c.ComponentItemId != Guid.Empty && c.QuantityRequired > 0)
                    {
                        var reqQty = Math.Round(c.QuantityRequired * factor, 4);
                        var avgCost = await _stockPosting.GetAverageCostAsync(c.ComponentItemId, wo.WarehouseId, token);
                        var unitCost = avgCost > 0 ? avgCost : c.UnitCost;
                        totalMaterialCost += Math.Round(reqQty * unitCost, 2);

                        await _stockPosting.IssueStockAsync(new StockIssueRequest(
                            wo.LegalEntityId,
                            c.ComponentItemId,
                            reqQty,
                            completionDate,
                            wo.WorkOrderNumber,
                            $"Consumed for WO {wo.WorkOrderNumber} ({c.ComponentItemCode})",
                            wo.WarehouseId != Guid.Empty ? wo.WarehouseId : null,
                            SkipJournal: true), token);
                    }
                }
            }

            var actualTotalCost = totalMaterialCost + conversionCost;
            wo.ActualTotalCost = actualTotalCost;

            if (wo.CompletedQuantity > 0 && wo.ProducedItemId != Guid.Empty)
            {
                var finishedUnitCost = wo.CompletedQuantity > 0 ? Math.Round(actualTotalCost / wo.CompletedQuantity, 4) : 0m;
                await _stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                    wo.LegalEntityId,
                    wo.ProducedItemId,
                    wo.CompletedQuantity,
                    finishedUnitCost,
                    completionDate,
                    wo.WorkOrderNumber,
                    $"Produced via WO {wo.WorkOrderNumber} ({wo.ProducedItemCode})",
                    wo.WarehouseId != Guid.Empty ? wo.WarehouseId : null,
                    SkipJournal: true), token);
            }

            // Post one balanced journal group:
            // Dr Finished-goods inventory, Cr Raw-material inventory (components) and Cr WIP/absorption for conversion cost.
            if (actualTotalCost > 0)
            {
                var invAccountId = await _posting.AccountForRoleAsync("inventory", token);
                Guid wipAccountId;
                try { wipAccountId = await _posting.AccountForRoleAsync(SystemRoles.Wip, token); }
                catch
                {
                    try { wipAccountId = await _posting.AccountForRoleAsync(SystemRoles.InventoryClearing, token); }
                    catch { wipAccountId = await _posting.AccountForRoleAsync("inventory", token); }
                }

                var lines = new List<PostingLine>
                {
                    new(invAccountId, actualTotalCost, 0, $"Finished goods produced - WO {wo.WorkOrderNumber}")
                };

                if (totalMaterialCost > 0)
                {
                    lines.Add(new(invAccountId, 0, totalMaterialCost, $"Raw materials consumed - WO {wo.WorkOrderNumber}"));
                }

                if (conversionCost > 0)
                {
                    lines.Add(new(wipAccountId, 0, conversionCost, $"Conversion cost absorption - WO {wo.WorkOrderNumber}"));
                }

                await _posting.PostAsync(new PostingRequest(
                    wo.LegalEntityId,
                    completionDate,
                    "manufacturing",
                    wo.Id,
                    $"Production completion WO {wo.WorkOrderNumber}",
                    wo.WorkOrderNumber,
                    lines), token);
            }

            await _orders.SaveChangesAsync(token);
            return Map(wo);
        }, cancellationToken);
    }

    public async Task<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var wo = await _orders.GetForUpdateAsync(request.Id, cancellationToken);
        if (wo.Status != WorkOrderStatus.Planned && wo.Status != WorkOrderStatus.InProgress)
            throw new ConflictException($"Cannot cancel a Work Order in status {wo.Status}.");
        if (wo.CompletedQuantity > 0)
            throw new ConflictException("Cannot cancel a Work Order with completed quantity.");

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
