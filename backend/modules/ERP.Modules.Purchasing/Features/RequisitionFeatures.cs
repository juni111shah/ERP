using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Modules.Purchasing.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

public sealed record RequisitionLineInput(
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal EstimatedUnitPrice);

public sealed record RequisitionInput(
    Guid LegalEntityId,
    string? Number,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    string? Reason,
    List<RequisitionLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record RequisitionLineDto(
    Guid Id,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal EstimatedUnitPrice,
    decimal LineTotal);

public sealed record RequisitionSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    PurchaseRequisitionStatus Status,
    decimal EstimatedTotal,
    int LineCount,
    Guid? ConvertedPurchaseOrderId,
    DateTime CreatedAt);

public sealed record RequisitionDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    string RequesterName,
    string? Department,
    DateOnly RequisitionDate,
    DateOnly? RequiredDate,
    PurchaseRequisitionStatus Status,
    decimal EstimatedTotal,
    string? Reason,
    Guid? ConvertedPurchaseOrderId,
    IReadOnlyList<RequisitionLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt);

public sealed record GetRequisition(Guid Id) : IQuery<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsRead; }
public sealed record CreateRequisition(RequisitionInput Input) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
public sealed record UpdateRequisition(Guid Id, RequisitionInput Input) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
public sealed record DeleteRequisition(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
public sealed record ChangeRequisitionStatus(Guid Id, PurchaseRequisitionStatus Status) : ICommand<RequisitionDto>, IRequirePermission { public string RequiredPermission => P.RequisitionsManage; }
public sealed record ConvertRequisitionToOrder(Guid Id, Guid? VendorPartyId, string VendorName) : ICommand<PurchaseOrderDto>, IRequirePermission { public string RequiredPermission => P.OrdersManage; }

public sealed class RequisitionInputValidator : AbstractValidator<RequisitionInput>
{
    public RequisitionInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.RequesterName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Requisition must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
            l.RuleFor(x => x.Quantity).GreaterThan(0);
            l.RuleFor(x => x.EstimatedUnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CreateRequisitionValidator : AbstractValidator<CreateRequisition>
{
    public CreateRequisitionValidator() => RuleFor(x => x.Input).SetValidator(new RequisitionInputValidator());
}

public sealed class UpdateRequisitionValidator : AbstractValidator<UpdateRequisition>
{
    public UpdateRequisitionValidator() => RuleFor(x => x.Input).SetValidator(new RequisitionInputValidator());
}

internal sealed class RequisitionHandlers(
    IRepository<PurchaseRequisition> requisitions,
    IRepository<PurchaseOrder> orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) :
    IRequestHandler<GetRequisition, RequisitionDto>,
    IRequestHandler<CreateRequisition, RequisitionDto>,
    IRequestHandler<UpdateRequisition, RequisitionDto>,
    IRequestHandler<DeleteRequisition, Unit>,
    IRequestHandler<ChangeRequisitionStatus, RequisitionDto>,
    IRequestHandler<ConvertRequisitionToOrder, PurchaseOrderDto>
{
    public async Task<RequisitionDto> Handle(GetRequisition q, CancellationToken ct)
    {
        var req = await requisitions.Tracked(new Specification<PurchaseRequisition>(x => x.Id == q.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Requisition '{q.Id}' not found.");
        return await ToDtoAsync(req, ct);
    }

    public async Task<RequisitionDto> Handle(CreateRequisition cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var req = new PurchaseRequisition
        {
            LegalEntityId = entity.Id,
            RequesterName = i.RequesterName.Trim(),
            Department = i.Department?.Trim(),
            RequisitionDate = i.RequisitionDate,
            RequiredDate = i.RequiredDate,
            Reason = i.Reason,
            Status = PurchaseRequisitionStatus.Draft,
        };

        req.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.PurchaseRequisition, entity.Id, i.RequisitionDate, ct)
            : i.Number.Trim();

        if (await requisitions.AnyAsync(x => x.Number == req.Number, ct))
            throw new ConflictException($"Requisition number '{req.Number}' already exists.");

        PopulateLinesAndTotals(req, i.Lines);
        await fields.ApplyAsync(Docs.PurchaseRequisition, req, i.CustomFields, isCreate: true, ct);

        requisitions.Add(req);
        await requisitions.SaveChangesAsync(ct);
        return await ToDtoAsync(req, ct);
    }

    public async Task<RequisitionDto> Handle(UpdateRequisition cmd, CancellationToken ct)
    {
        var req = await requisitions.Tracked(new Specification<PurchaseRequisition>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        if (req.Status == PurchaseRequisitionStatus.Ordered)
            throw new ConflictException("Cannot edit an ordered requisition.");

        var i = cmd.Input;
        req.RequesterName = i.RequesterName.Trim();
        req.Department = i.Department?.Trim();
        req.RequisitionDate = i.RequisitionDate;
        req.RequiredDate = i.RequiredDate;
        req.Reason = i.Reason;

        PopulateLinesAndTotals(req, i.Lines);
        await fields.ApplyAsync(Docs.PurchaseRequisition, req, i.CustomFields, isCreate: false, ct);

        await requisitions.SaveChangesAsync(ct);
        return await ToDtoAsync(req, ct);
    }

    public async Task<Unit> Handle(DeleteRequisition cmd, CancellationToken ct)
    {
        var req = await requisitions.GetForUpdateAsync(cmd.Id, ct);
        if (req.Status == PurchaseRequisitionStatus.Ordered)
            throw new ConflictException("Cannot delete an ordered requisition.");

        requisitions.Remove(req);
        await requisitions.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<RequisitionDto> Handle(ChangeRequisitionStatus cmd, CancellationToken ct)
    {
        var req = await requisitions.Tracked(new Specification<PurchaseRequisition>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        PurchasingTransitions.Requisition.Ensure(req.Status, cmd.Status, isSystem: false);

        req.Status = cmd.Status;
        await requisitions.SaveChangesAsync(ct);
        return await ToDtoAsync(req, ct);
    }

    public async Task<PurchaseOrderDto> Handle(ConvertRequisitionToOrder cmd, CancellationToken ct)
    {
        var req = await requisitions.Tracked(new Specification<PurchaseRequisition>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Requisition '{cmd.Id}' not found.");

        PurchasingTransitions.Requisition.Ensure(req.Status, PurchaseRequisitionStatus.Ordered, isSystem: true);

        var order = new PurchaseOrder
        {
            LegalEntityId = req.LegalEntityId,
            RequisitionId = req.Id,
            VendorPartyId = cmd.VendorPartyId,
            VendorName = string.IsNullOrWhiteSpace(cmd.VendorName) ? "Unassigned Vendor" : cmd.VendorName.Trim(),
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ExpectedDeliveryDate = req.RequiredDate,
            Currency = "QAR",
            Status = PurchaseOrderStatus.Draft,
            Notes = req.Reason,
            Number = await numbers.NextAsync(Docs.PurchaseOrder, req.LegalEntityId, DateOnly.FromDateTime(DateTime.UtcNow), ct),
        };

        decimal subtotal = 0;
        foreach (var l in req.Lines)
        {
            var lineTotal = l.Quantity * l.EstimatedUnitPrice;
            subtotal += lineTotal;
            order.Lines.Add(new PurchaseOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.EstimatedUnitPrice,
                LineTotal = lineTotal
            });
        }

        order.SubTotal = subtotal;
        order.TotalAmount = subtotal;

        orders.Add(order);
        req.Status = PurchaseRequisitionStatus.Ordered;
        req.ConvertedPurchaseOrderId = order.Id;

        await requisitions.SaveChangesAsync(ct);
        await orders.SaveChangesAsync(ct);

        return await PurchaseOrderHandlers.ToDtoAsync(order, fields, ct);
    }

    private static void PopulateLinesAndTotals(PurchaseRequisition req, List<RequisitionLineInput> lines)
    {
        req.Lines.Clear();
        decimal total = 0;

        foreach (var l in lines)
        {
            var lineTotal = Math.Round(l.Quantity * l.EstimatedUnitPrice, 2);
            total += lineTotal;

            req.Lines.Add(new PurchaseRequisitionLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                Quantity = l.Quantity,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim(),
                EstimatedUnitPrice = l.EstimatedUnitPrice,
                LineTotal = lineTotal
            });
        }

        req.EstimatedTotal = total;
    }

    private async Task<RequisitionDto> ToDtoAsync(PurchaseRequisition r, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.PurchaseRequisition, r, ct);
        return new RequisitionDto(
            r.Id,
            r.Number,
            r.LegalEntityId,
            r.RequesterName,
            r.Department,
            r.RequisitionDate,
            r.RequiredDate,
            r.Status,
            r.EstimatedTotal,
            r.Reason,
            r.ConvertedPurchaseOrderId,
            r.Lines.Select(l => new RequisitionLineDto(
                l.Id, l.ItemId, l.ItemCode, l.Description, l.Quantity, l.UnitOfMeasure,
                l.EstimatedUnitPrice, l.LineTotal)).ToList(),
            cf,
            r.CreatedAt);
    }
}
