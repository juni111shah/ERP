using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
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

public record WorkOrderMaterialIssueDto(
    Guid Id,
    Guid ItemId,
    string ItemCode,
    string ItemName,
    decimal Quantity,
    decimal UnitCost,
    decimal Value,
    DateOnly IssueDate,
    bool Backflushed,
    string? Notes,
    DateTimeOffset? ReversedAt);

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
    DateTimeOffset CreatedAt,
    decimal MaterialCost = 0,
    decimal ConversionCost = 0,
    decimal ScrapCost = 0,
    List<WorkOrderMaterialIssueDto>? MaterialIssues = null,
    DateTimeOffset? ReversedAt = null,
    Guid? ReversedByUserId = null,
    string? ReversalReason = null);

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

/// <summary>IssueMaterials overrides the <c>manufacturing.issueMaterialsOnRelease</c> setting for this release.</summary>
public record ReleaseWorkOrderCommand(Guid Id, bool? IssueMaterials = null) : ICommand<WorkOrderDto>;

public record MaterialLineInput(Guid ItemId, decimal Quantity, string? Notes = null);

/// <summary>Puts material into the work order: Dr WIP, Cr inventory at the stock service's average cost. Without lines it issues what the BOM still needs for the planned quantity.</summary>
public record IssueWorkOrderMaterialsCommand(
    Guid WorkOrderId,
    List<MaterialLineInput>? Lines = null,
    DateOnly? Date = null,
    string? Notes = null) : ICommand<WorkOrderDto>;

/// <summary>Sends unused material back to stock at the cost it was issued at: Dr inventory, Cr WIP.</summary>
public record ReturnWorkOrderMaterialsCommand(
    Guid WorkOrderId,
    List<MaterialLineInput> Lines,
    DateOnly? Date = null,
    string? Notes = null) : ICommand<WorkOrderDto>;

public record RecordOperationHoursCommand(
    Guid WorkOrderId,
    Guid OperationId,
    decimal ActualHours,
    OperationStatus? Status = null) : ICommand<WorkOrderDto>;

public record OperationHoursInput(Guid OperationId, decimal ActualHours);

/// <summary>ActualTotalCost is kept for older callers and ignored: the cost comes from what was issued and absorbed.</summary>
public record CompleteWorkOrderCommand(
    Guid Id,
    decimal CompletedQuantity,
    decimal ScrappedQuantity,
    decimal ActualTotalCost,
    string? Notes,
    DateOnly? CompletionDate = null,
    List<OperationHoursInput>? OperationHours = null) : ICommand<WorkOrderDto>;

public record CancelWorkOrderCommand(Guid Id, string? Reason) : ICommand<Unit>;

public record ReverseWorkOrderCommand(Guid Id, string Reason) : ICommand<WorkOrderDto>;

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

internal static class MaterialLineRules
{
    public static void Apply(InlineValidator<MaterialLineInput> l)
    {
        l.RuleFor(x => x.ItemId).NotEmpty();
        l.RuleFor(x => x.Quantity).GreaterThan(0).Must(v => decimal.Round(v, 4) == v).WithMessage("Use at most 4 decimals.");
        l.RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class IssueWorkOrderMaterialsValidator : AbstractValidator<IssueWorkOrderMaterialsCommand>
{
    public IssueWorkOrderMaterialsValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleForEach(x => x.Lines).ChildRules(MaterialLineRules.Apply);
    }
}

public class ReturnWorkOrderMaterialsValidator : AbstractValidator<ReturnWorkOrderMaterialsCommand>
{
    public ReturnWorkOrderMaterialsValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose at least one item to return.");
        RuleForEach(x => x.Lines).ChildRules(MaterialLineRules.Apply);
    }
}

public class RecordOperationHoursValidator : AbstractValidator<RecordOperationHoursCommand>
{
    public RecordOperationHoursValidator()
    {
        RuleFor(x => x.WorkOrderId).NotEmpty();
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class CompleteWorkOrderValidator : AbstractValidator<CompleteWorkOrderCommand>
{
    public CompleteWorkOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CompletedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ScrappedQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ActualTotalCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.CompletedQuantity + x.ScrappedQuantity > 0).WithName("CompletedQuantity").WithMessage("Enter what was made or scrapped. To stop an order that made nothing, cancel it.");
        RuleForEach(x => x.OperationHours).ChildRules(o =>
        {
            o.RuleFor(x => x.OperationId).NotEmpty();
            o.RuleFor(x => x.ActualHours).GreaterThanOrEqualTo(0);
        });
    }
}

public class ReverseWorkOrderValidator : AbstractValidator<ReverseWorkOrderCommand>
{
    public ReverseWorkOrderValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Reason).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Say why this work order is being reversed.").MaximumLength(500);
    }
}

public class WorkOrderHandlers :
    IRequestHandler<CreateWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<ReleaseWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<IssueWorkOrderMaterialsCommand, WorkOrderDto>,
    IRequestHandler<ReturnWorkOrderMaterialsCommand, WorkOrderDto>,
    IRequestHandler<RecordOperationHoursCommand, WorkOrderDto>,
    IRequestHandler<CompleteWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<CancelWorkOrderCommand, Unit>,
    IRequestHandler<ReverseWorkOrderCommand, WorkOrderDto>,
    IRequestHandler<GetWorkOrderByIdQuery, WorkOrderDto?>
{
    /// <summary>Takes the credit for labour and overhead moved into WIP. Falls back to the general expense role in a workspace that has not set one up.</summary>
    public const string AbsorptionRole = "manufacturing.absorption";
    /// <summary>Takes the cost of scrapped units. Falls back to cost of goods sold.</summary>
    public const string ScrapRole = "manufacturing.scrap";
    private const string JournalSource = "manufacturing";

    private readonly IRepository<WorkOrder> _orders;
    private readonly IRepository<WorkOrderMaterialIssue> _issues;
    private readonly IRepository<BillOfMaterials> _boms;
    private readonly INumberingService _numbering;
    private readonly IStockPostingService _stockPosting;
    private readonly IPostingService _posting;
    private readonly IRepository<JournalEntry> _journals;
    private readonly IRepository<AuditEvent> _audit;
    private readonly IReadRepository<Item> _items;
    private readonly ISettingsService _settings;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ERP.Application.Organizations.ILegalEntityGuard _guard;

    public WorkOrderHandlers(
        IRepository<WorkOrder> orders,
        IRepository<WorkOrderMaterialIssue> issues,
        IRepository<BillOfMaterials> boms,
        INumberingService numbering,
        IStockPostingService stockPosting,
        IPostingService posting,
        IRepository<JournalEntry> journals,
        IRepository<AuditEvent> audit,
        IReadRepository<Item> items,
        ISettingsService settings,
        ICurrentUser user,
        IClock clock,
        IUnitOfWork unitOfWork,
        ERP.Application.Organizations.ILegalEntityGuard guard)
    {
        _guard = guard;
        _orders = orders;
        _issues = issues;
        _boms = boms;
        _numbering = numbering;
        _stockPosting = stockPosting;
        _posting = posting;
        _journals = journals;
        _audit = audit;
        _items = items;
        _settings = settings;
        _user = user;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    private static DateOnly Today => DateOnly.FromDateTime(AppClock.UtcNow);

    private static DateOnly DateOrToday(DateOnly? date, string field)
    {
        var today = Today;
        if (date is { } d && d > today) throw new FieldValidationException(field, "The date cannot be in the future.");
        return date ?? today;
    }

    private async Task<WorkOrder> LoadAsync(Guid id, CancellationToken ct)
        => await _orders.Tracked(new Specification<WorkOrder>(w => w.Id == id))
            .Include(w => w.Operations)
            .Include(w => w.MaterialIssues)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Work Order {id} not found.");

    private async Task<BillOfMaterials> LoadBomAsync(Guid bomId, CancellationToken ct)
        => await _boms.Query(new Specification<BillOfMaterials>(b => b.Id == bomId))
            .Include(b => b.Components)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"BOM {bomId} not found.");

    public async Task<WorkOrderDto> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var bom = await LoadBomAsync(request.BomId, cancellationToken);

        await _guard.RequireAsync(request.LegalEntityId, cancellationToken);
        if (bom.LegalEntityId != request.LegalEntityId) throw new FieldValidationException("bomId", "This BOM belongs to another legal entity.");
        if (!bom.IsActive) throw new ConflictException($"BOM {bom.Code} is inactive.");
        if (!await _stockPosting.IsWarehouseUsableAsync(request.WarehouseId, request.LegalEntityId, cancellationToken))
            throw new FieldValidationException("warehouseId", "Choose an active warehouse of this legal entity.");

        var number = await _numbering.NextAsync("WorkOrder", request.LegalEntityId, request.StartDate, cancellationToken);

        // Estimate at today's component costs in the warehouse the order will draw from.
        var costs = await BomStructure.CurrentCostsAsync(bom.Components, _stockPosting, request.WarehouseId, cancellationToken);
        var totalMaterial = BomStructure.MaterialCost(bom.Components, costs);
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

    public Task<WorkOrderDto> Handle(ReleaseWorkOrderCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.InProgress, isSystem: false);
        wo.Status = WorkOrderStatus.InProgress;

        var issue = request.IssueMaterials ?? await _settings.GetAsync<bool?>("manufacturing.issueMaterialsOnRelease", wo.LegalEntityId, token) ?? false;
        if (issue)
        {
            var bom = await LoadBomAsync(wo.BomId, token);
            await IssueAsync(wo, await BomNeedsAsync(wo, bom, wo.PlannedQuantity, token), Today, backflushed: false, $"Issued on release of {wo.WorkOrderNumber}", token);
        }

        await _orders.SaveChangesAsync(token);
        return Map(wo);
    }, cancellationToken);

    public Task<WorkOrderDto> Handle(IssueWorkOrderMaterialsCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.WorkOrderId, token);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Release the work order before issuing material to it.");
        var date = DateOrToday(request.Date, "date");

        List<MaterialNeed> needs;
        if (request.Lines is { Count: > 0 })
        {
            // Actual consumption can differ from the BOM, so any stocked product can be issued, in any amount.
            var ids = request.Lines.Select(l => l.ItemId).Distinct().ToList();
            var items = await ProductsAsync(ids, token);
            var missing = ids.FirstOrDefault(id => !items.ContainsKey(id));
            if (missing != Guid.Empty) throw new FieldValidationException("lines", $"Item {missing} was not found or is not a product.");
            needs = request.Lines.Select(l => new MaterialNeed(l.ItemId, items[l.ItemId].Sku, items[l.ItemId].Name, l.Quantity, l.Notes)).ToList();
        }
        else
        {
            var bom = await LoadBomAsync(wo.BomId, token);
            needs = await BomNeedsAsync(wo, bom, wo.PlannedQuantity, token);
            if (needs.Count == 0) throw new ConflictException("The bill of materials for this order is already fully issued.");
        }

        await IssueAsync(wo, needs, date, backflushed: false, request.Notes, token);
        await _orders.SaveChangesAsync(token);
        return Map(wo);
    }, cancellationToken);

    public Task<WorkOrderDto> Handle(ReturnWorkOrderMaterialsCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.WorkOrderId, token);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Only material on a work order in progress can be returned.");
        var date = DateOrToday(request.Date, "date");
        var memo = $"Returned from WO {wo.WorkOrderNumber}";
        var net = NetIssued(wo);

        decimal total = 0;
        foreach (var line in request.Lines.GroupBy(l => l.ItemId))
        {
            var quantity = line.Sum(l => l.Quantity);
            if (!net.TryGetValue(line.Key, out var issued) || quantity > issued.Quantity)
                throw new ConflictException($"Only {(net.TryGetValue(line.Key, out var had) ? had.Quantity : 0m):0.####} of that item is on this work order.");

            var unitCost = Math.Round(issued.Value / issued.Quantity, 4);
            var moved = await _stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                wo.LegalEntityId, line.Key, quantity, unitCost, date, $"{wo.WorkOrderNumber}-RET", memo, wo.WarehouseId, SkipJournal: true), token);
            if (moved is null) throw new ConflictException("Stock is not being tracked, so nothing can be returned. Is the Inventory module on?");

            var value = quantity == issued.Quantity ? issued.Value : Math.Round(quantity * unitCost, 2); // the last unit takes what is left, so WIP lands on zero
            total += value;
            var first = wo.MaterialIssues.First(i => i.ItemId == line.Key);
            _issues.Add(new WorkOrderMaterialIssue
            {
                LegalEntityId = wo.LegalEntityId,
                WorkOrderId = wo.Id,
                ItemId = line.Key,
                ItemCode = first.ItemCode,
                ItemName = first.ItemName,
                Quantity = -quantity,
                UnitCost = unitCost,
                Value = -value,
                IssueDate = date,
                Notes = request.Notes?.Trim() ?? line.First().Notes?.Trim(),
            });
        }

        if (total > 0)
        {
            await _posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, JournalSource, wo.Id, memo, wo.WorkOrderNumber,
            [
                new(await _posting.AccountForRoleAsync("inventory", token), total, 0, memo),
                new(await _posting.AccountForRoleAsync(SystemRoles.Wip, token), 0, total, memo),
            ]), token);
        }

        await _orders.SaveChangesAsync(token);
        return Map(wo);
    }, cancellationToken);

    public async Task<WorkOrderDto> Handle(RecordOperationHoursCommand request, CancellationToken cancellationToken)
    {
        var wo = await LoadAsync(request.WorkOrderId, cancellationToken);
        if (wo.Status != WorkOrderStatus.InProgress) throw new ConflictException("Hours can only be recorded while the work order is in progress.");
        var op = wo.Operations.FirstOrDefault(o => o.Id == request.OperationId) ?? throw new NotFoundException($"Operation {request.OperationId} is not on this work order.");
        op.ActualHours = request.ActualHours;
        op.Status = request.Status ?? (request.ActualHours > 0 && op.Status == OperationStatus.Pending ? OperationStatus.InProgress : op.Status);
        await _orders.SaveChangesAsync(cancellationToken);
        return Map(wo);
    }

    public Task<WorkOrderDto> Handle(CompleteWorkOrderCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Completed, isSystem: false);

        var made = request.CompletedQuantity;
        var scrapped = request.ScrappedQuantity;
        var overPercent = await _settings.GetAsync<decimal?>("manufacturing.overCompletionPercent", wo.LegalEntityId, token) ?? 0m;
        var allowed = Math.Round(wo.PlannedQuantity * (1 + overPercent / 100m), 4);
        if (made + scrapped > allowed)
            throw new FieldValidationException("completedQuantity", $"This order is for {wo.PlannedQuantity:0.####}; made and scrapped together cannot pass {allowed:0.####}.");

        var date = DateOrToday(request.CompletionDate, "completionDate");
        var stockOn = await _stockPosting.IsInventoryEnabledAsync(token);
        var bom = await LoadBomAsync(wo.BomId, token);
        var backflush = await _settings.GetAsync<bool?>("manufacturing.autoConsumeComponents", wo.LegalEntityId, token) ?? true;

        var producedType = (await _items.Query(new Specification<Item>(i => i.Id == wo.ProducedItemId)).Select(i => new { i.Type }).FirstOrDefaultAsync(token))?.Type;
        if (stockOn && made > 0 && producedType != ItemType.Product)
            throw new ConflictException($"'{wo.ProducedItemCode}' is not a stocked product, so the finished goods cannot be received.");

        foreach (var hours in request.OperationHours ?? [])
        {
            var op = wo.Operations.FirstOrDefault(o => o.Id == hours.OperationId) ?? throw new FieldValidationException("operationHours", "That operation is not on this work order.");
            op.ActualHours = hours.ActualHours;
        }
        foreach (var op in wo.Operations) op.Status = OperationStatus.Completed;

        if (backflush)
        {
            var needs = await BomNeedsAsync(wo, bom, made + scrapped, token);
            await IssueAsync(wo, needs, date, backflushed: true, null, token);
        }

        wo.CompletionDate = date;
        wo.Status = WorkOrderStatus.Completed;
        wo.CompletedQuantity = made;
        wo.ScrappedQuantity = scrapped;
        if (!string.IsNullOrWhiteSpace(request.Notes)) wo.Notes = (wo.Notes + " | Completed: " + request.Notes).Trim();

        // Conversion is what the operations took (actual hours where recorded), else the BOM's labour and overhead for every unit worked on.
        var opCost = Math.Round(wo.Operations.Sum(o => (o.ActualHours > 0 ? o.ActualHours : o.PlannedHours) * o.HourlyRate), 2);
        var conversion = opCost > 0 ? opCost : Math.Round((bom.LaborCostPerUnit + bom.OverheadCostPerUnit) * (made + scrapped), 2);
        var material = NetIssued(wo).Values.Sum(v => v.Value);
        var total = material + conversion;
        var goodValue = Math.Round(total * made / (made + scrapped), 2);
        var scrapValue = total - goodValue;

        wo.MaterialCost = material;
        wo.ConversionCost = conversion;
        wo.ScrapCost = scrapValue;
        wo.ActualTotalCost = total;

        // With Inventory off nothing was received or issued, so there is no stock for the ledger to carry: the cost is recorded on the order only.
        if (stockOn)
        {
            if (made > 0)
            {
                await _stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                    wo.LegalEntityId, wo.ProducedItemId, made, Math.Round(goodValue / made, 4), date, wo.WorkOrderNumber,
                    $"Produced via WO {wo.WorkOrderNumber} ({wo.ProducedItemCode})", wo.WarehouseId, SkipJournal: true), token);
            }

            if (total > 0)
            {
                var wip = await _posting.AccountForRoleAsync(SystemRoles.Wip, token);
                var lines = new List<PostingLine>();
                if (conversion > 0)
                {
                    lines.Add(new(wip, conversion, 0, $"Conversion cost into WIP - WO {wo.WorkOrderNumber}"));
                    lines.Add(new(await RoleOrFallbackAsync(AbsorptionRole, SystemRoles.PurchasesExpense, token), 0, conversion, $"Conversion cost absorbed - WO {wo.WorkOrderNumber}"));
                }
                if (goodValue > 0) lines.Add(new(await _posting.AccountForRoleAsync("inventory", token), goodValue, 0, $"Finished goods produced - WO {wo.WorkOrderNumber}"));
                if (scrapValue > 0) lines.Add(new(await RoleOrFallbackAsync(ScrapRole, "cogs", token), scrapValue, 0, $"Scrapped units - WO {wo.WorkOrderNumber}"));
                lines.Add(new(wip, 0, total, $"WIP released - WO {wo.WorkOrderNumber}"));

                await _posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, JournalSource, wo.Id, $"Production completion WO {wo.WorkOrderNumber}", wo.WorkOrderNumber, lines), token);
            }
        }

        await _orders.SaveChangesAsync(token);
        return Map(wo);
    }, cancellationToken);

    public Task<Unit> Handle(CancelWorkOrderCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Cancelled, isSystem: false);
        if (wo.CompletedQuantity > 0)
            throw new ConflictException("Cannot cancel a Work Order with completed quantity.");

        var memo = $"Cancelled WO {wo.WorkOrderNumber}";
        await UndoMaterialAsync(wo, memo, token);
        await ReverseJournalsAsync(wo, memo, token);

        wo.Status = WorkOrderStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            wo.Notes = (wo.Notes + " | Cancelled: " + request.Reason).Trim();
        }
        await _orders.SaveChangesAsync(token);
        return Unit.Value;
    }, cancellationToken);

    public Task<WorkOrderDto> Handle(ReverseWorkOrderCommand request, CancellationToken cancellationToken) => _unitOfWork.InTransactionAsync(async token =>
    {
        var wo = await LoadAsync(request.Id, token);
        ManufacturingTransitions.WorkOrder.Ensure(wo.Status, WorkOrderStatus.Reversed, isSystem: false);
        var reason = request.Reason.Trim();
        var memo = $"Reversal of WO {wo.WorkOrderNumber}: {reason}";
        if (memo.Length > VoidMemo.MaxMemo) memo = memo[..(VoidMemo.MaxMemo - 3)] + "...";

        if (await _stockPosting.IsInventoryEnabledAsync(token))
        {
            // The stock service takes the receipt back only if on-hand still covers it. An order completed with Inventory off has no receipt, and this does nothing.
            try { await _stockPosting.ReverseReceiptAsync(wo.WorkOrderNumber, token); }
            catch (ConflictException)
            {
                throw new ConflictException($"Some of the {wo.CompletedQuantity:0.####} {wo.ProducedItemCode} this order produced have left stock, so it can no longer be reversed.");
            }
        }
        else if (await _journals.AnyAsync(j => j.SourceType == JournalSource && j.SourceId == wo.Id && j.Status == JournalStatus.Posted, token))
        {
            throw new ConflictException("Turn the Inventory module on first: this work order moved stock that has to go back.");
        }

        await UndoMaterialAsync(wo, memo, token);
        await ReverseJournalsAsync(wo, memo, token);

        wo.Status = WorkOrderStatus.Reversed;
        wo.ReversedAt = _clock.UtcNow;
        wo.ReversedByUserId = _user.UserId;
        wo.ReversalReason = reason;
        _audit.Add(new AuditEvent
        {
            OccurredAt = _clock.UtcNow,
            UserId = _user.UserId,
            Action = "Reversed",
            RecordType = nameof(WorkOrder),
            RecordId = wo.Id.ToString(),
            ChangedFields = "Status",
            After = System.Text.Json.JsonSerializer.Serialize(new { number = wo.WorkOrderNumber, reason }),
            IpAddress = _user.IpAddress,
        });

        await _orders.SaveChangesAsync(token);
        return Map(wo);
    }, cancellationToken);

    public async Task<WorkOrderDto?> Handle(GetWorkOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var wo = await _orders.Query(new Specification<WorkOrder>(w => w.Id == request.Id))
            .Include(w => w.Operations)
            .Include(w => w.MaterialIssues)
            .FirstOrDefaultAsync(cancellationToken);

        return wo is null ? null : Map(wo);
    }

    private sealed record MaterialNeed(Guid ItemId, string Code, string Name, decimal Quantity, string? Notes = null);

    private async Task<Guid> RoleOrFallbackAsync(string role, string fallback, CancellationToken ct)
    {
        try { return await _posting.AccountForRoleAsync(role, ct); }
        catch (ConflictException) { return await _posting.AccountForRoleAsync(fallback, ct); }
    }

    private async Task<Dictionary<Guid, Item>> ProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => await _items.Query(new Specification<Item>(i => ids.Contains(i.Id) && i.Type == ItemType.Product)).ToDictionaryAsync(i => i.Id, ct);

    /// <summary>What is on the order now, per item: issues less returns, leaving out anything cancelled or reversed.</summary>
    private static Dictionary<Guid, (decimal Quantity, decimal Value)> NetIssued(WorkOrder wo)
        => wo.MaterialIssues.Where(i => i.ReversedAt is null).GroupBy(i => i.ItemId)
            .ToDictionary(g => g.Key, g => (g.Sum(i => i.Quantity), g.Sum(i => i.Value)));

    /// <summary>What the BOM still needs for this output, wastage included, less what has already been issued. Every component must be a stocked product.</summary>
    private async Task<List<MaterialNeed>> BomNeedsAsync(WorkOrder wo, BillOfMaterials bom, decimal output, CancellationToken ct)
    {
        var ids = bom.Components.Select(c => c.ComponentItemId).Distinct().ToList();
        var products = await ProductsAsync(ids, ct);
        var net = NetIssued(wo);
        var needs = new List<MaterialNeed>();
        foreach (var c in bom.Components)
        {
            if (!products.ContainsKey(c.ComponentItemId))
                throw new ConflictException($"Component item '{c.ComponentItemCode}' was not found or is not a Product.");
            var still = BomStructure.Required(bom, c, output) - (net.TryGetValue(c.ComponentItemId, out var issued) ? issued.Quantity : 0m);
            if (still > 0) needs.Add(new MaterialNeed(c.ComponentItemId, c.ComponentItemCode, c.ComponentItemName, still));
        }
        return needs;
    }

    /// <summary>Refuses to take a tracked item below zero unless the setting allows it. Items the stock service does not track are left out of on-hand and pass.</summary>
    private async Task EnsureAvailableAsync(WorkOrder wo, IReadOnlyList<MaterialNeed> needs, CancellationToken ct)
    {
        if (await _settings.GetAsync<bool?>("manufacturing.allowNegativeStock", wo.LegalEntityId, ct) == true) return;
        var wanted = needs.GroupBy(n => n.ItemId).ToDictionary(g => g.Key, g => g.Sum(n => n.Quantity));
        var onHand = await _stockPosting.GetOnHandAsync(wanted.Keys.ToList(), wo.WarehouseId, wo.LegalEntityId, ct);
        var shortages = wanted.Where(w => onHand.TryGetValue(w.Key, out var have) && have < w.Value)
            .Select(w => $"{needs.First(n => n.ItemId == w.Key).Code}: need {w.Value:0.####}, {onHand[w.Key]:0.####} on hand").ToList();
        if (shortages.Count > 0)
            throw new ConflictException($"Not enough stock in this warehouse. {string.Join("; ", shortages)}.");
    }

    /// <summary>Dr WIP, Cr inventory, at the cost the stock service issues at. A line that moved no stock (Inventory off) posts nothing.</summary>
    private async Task IssueAsync(WorkOrder wo, IReadOnlyList<MaterialNeed> needs, DateOnly date, bool backflushed, string? notes, CancellationToken ct)
    {
        if (needs.Count == 0) return;
        await EnsureAvailableAsync(wo, needs, ct);

        decimal total = 0;
        foreach (var need in needs)
        {
            var unitCost = await _stockPosting.GetAverageCostAsync(need.ItemId, wo.WarehouseId, ct);
            var moved = await _stockPosting.IssueStockAsync(new StockIssueRequest(
                wo.LegalEntityId, need.ItemId, need.Quantity, date, wo.WorkOrderNumber,
                $"Consumed for WO {wo.WorkOrderNumber} ({need.Code})", wo.WarehouseId, SkipJournal: true), ct);
            if (moved is null) continue;

            var value = Math.Round(need.Quantity * unitCost, 2);
            total += value;
            _issues.Add(new WorkOrderMaterialIssue
            {
                LegalEntityId = wo.LegalEntityId,
                WorkOrderId = wo.Id,
                ItemId = need.ItemId,
                ItemCode = need.Code,
                ItemName = need.Name,
                Quantity = need.Quantity,
                UnitCost = unitCost,
                Value = value,
                IssueDate = date,
                Backflushed = backflushed,
                Notes = (need.Notes ?? notes)?.Trim(),
            });
        }

        if (total > 0)
        {
            var memo = $"Material issued to WO {wo.WorkOrderNumber}";
            await _posting.PostAsync(new PostingRequest(wo.LegalEntityId, date, JournalSource, wo.Id, memo, wo.WorkOrderNumber,
            [
                new(await _posting.AccountForRoleAsync(SystemRoles.Wip, ct), total, 0, memo),
                new(await _posting.AccountForRoleAsync("inventory", ct), 0, total, memo),
            ]), ct);
        }
    }

    /// <summary>Puts everything still on the order back on the shelf at the cost it left at. The ledger side is the reversal of the order's journals.</summary>
    private async Task UndoMaterialAsync(WorkOrder wo, string memo, CancellationToken ct)
    {
        var net = NetIssued(wo).Where(n => n.Value.Quantity > 0).ToList();
        if (net.Count > 0 && !await _stockPosting.IsInventoryEnabledAsync(ct))
            throw new ConflictException("Turn the Inventory module on first: this work order moved stock that has to go back.");

        foreach (var (itemId, issued) in net)
        {
            await _stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                wo.LegalEntityId, itemId, issued.Quantity, Math.Round(issued.Value / issued.Quantity, 4), Today,
                $"{wo.WorkOrderNumber}-REV", memo, wo.WarehouseId, SkipJournal: true), ct);
        }

        var now = _clock.UtcNow;
        foreach (var row in wo.MaterialIssues.Where(i => i.ReversedAt is null)) row.ReversedAt = now;
    }

    private async Task ReverseJournalsAsync(WorkOrder wo, string memo, CancellationToken ct)
    {
        var posted = await _journals.Tracked(new Specification<JournalEntry>(j => j.SourceType == JournalSource && j.SourceId == wo.Id && j.Status == JournalStatus.Posted).Include(j => j.Lines))
            .ToListAsync(ct);
        foreach (var journal in posted)
            await _posting.ReverseTodayAsync(journal, memo, ct);
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
        w.CreatedAt,
        w.MaterialCost,
        w.ConversionCost,
        w.ScrapCost,
        w.MaterialIssues.OrderBy(i => i.IssueDate).ThenBy(i => i.CreatedAt).Select(i => new WorkOrderMaterialIssueDto(
            i.Id, i.ItemId, i.ItemCode, i.ItemName, i.Quantity, i.UnitCost, i.Value, i.IssueDate, i.Backflushed, i.Notes, i.ReversedAt)).ToList(),
        w.ReversedAt,
        w.ReversedByUserId,
        w.ReversalReason);
}
