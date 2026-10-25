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

public sealed record GoodsReceiptLineInput(
    Guid PurchaseOrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal QuantityReceived,
    string UnitOfMeasure);

public sealed record GoodsReceiptInput(
    Guid LegalEntityId,
    string? Number,
    Guid PurchaseOrderId,
    string ReceivedBy,
    DateOnly ReceiptDate,
    string? DeliveryNoteNumber,
    string? Notes,
    List<GoodsReceiptLineInput> Lines,
    JsonObject? CustomFields = null);

public sealed record GoodsReceiptLineDto(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid? ItemId,
    string ItemCode,
    string Description,
    decimal QuantityReceived,
    string UnitOfMeasure);

public sealed record GoodsReceiptSummaryDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    string VendorName,
    DateOnly ReceiptDate,
    string ReceivedBy,
    string? DeliveryNoteNumber,
    int LineCount,
    DateTime CreatedAt);

public sealed record GoodsReceiptDto(
    Guid Id,
    string Number,
    Guid LegalEntityId,
    Guid PurchaseOrderId,
    string PurchaseOrderNumber,
    string VendorName,
    DateOnly ReceiptDate,
    string ReceivedBy,
    string? DeliveryNoteNumber,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineDto> Lines,
    JsonObject CustomFields,
    DateTime CreatedAt);

public sealed record GetGoodsReceipt(Guid Id) : IQuery<GoodsReceiptDto>, IRequirePermission { public string RequiredPermission => P.ReceiptsRead; }
public sealed record CreateGoodsReceipt(GoodsReceiptInput Input) : ICommand<GoodsReceiptDto>, IRequirePermission { public string RequiredPermission => P.ReceiptsManage; }
public sealed record DeleteGoodsReceipt(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ReceiptsManage; }

public sealed class GoodsReceiptInputValidator : AbstractValidator<GoodsReceiptInput>
{
    public GoodsReceiptInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.PurchaseOrderId).NotEmpty();
        RuleFor(x => x.ReceivedBy).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Goods receipt must have at least one line.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(x => x.Description).NotEmpty();
            l.RuleFor(x => x.QuantityReceived).GreaterThan(0);
        });
    }
}

public sealed class CreateGoodsReceiptValidator : AbstractValidator<CreateGoodsReceipt>
{
    public CreateGoodsReceiptValidator() => RuleFor(x => x.Input).SetValidator(new GoodsReceiptInputValidator());
}

internal sealed class ReceiptHandlers(
    IRepository<GoodsReceipt> receipts,
    IRepository<PurchaseOrder> orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IStockPostingService stockPosting) :
    IRequestHandler<GetGoodsReceipt, GoodsReceiptDto>,
    IRequestHandler<CreateGoodsReceipt, GoodsReceiptDto>,
    IRequestHandler<DeleteGoodsReceipt, Unit>
{
    public async Task<GoodsReceiptDto> Handle(GetGoodsReceipt q, CancellationToken ct)
    {
        var grn = await receipts.Tracked(new Specification<GoodsReceipt>(x => x.Id == q.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Goods receipt '{q.Id}' not found.");
        return await ToDtoAsync(grn, ct);
    }

    public async Task<GoodsReceiptDto> Handle(CreateGoodsReceipt cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == i.PurchaseOrderId))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Purchase order '{i.PurchaseOrderId}' not found.");

        var grn = new GoodsReceipt
        {
            LegalEntityId = entity.Id,
            PurchaseOrderId = order.Id,
            PurchaseOrderNumber = order.Number,
            VendorName = order.VendorName,
            ReceivedBy = i.ReceivedBy.Trim(),
            ReceiptDate = i.ReceiptDate,
            DeliveryNoteNumber = i.DeliveryNoteNumber?.Trim(),
            Notes = i.Notes,
        };

        grn.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.GoodsReceipt, entity.Id, i.ReceiptDate, ct)
            : i.Number.Trim();

        foreach (var l in i.Lines)
        {
            grn.Lines.Add(new GoodsReceiptLine
            {
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ItemId = l.ItemId,
                ItemCode = l.ItemCode.Trim(),
                Description = l.Description.Trim(),
                QuantityReceived = l.QuantityReceived,
                UnitOfMeasure = string.IsNullOrWhiteSpace(l.UnitOfMeasure) ? "EA" : l.UnitOfMeasure.Trim()
            });

            var poLine = order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId);
            if (poLine != null)
            {
                poLine.ReceivedQuantity += l.QuantityReceived;
            }

            if (l.ItemId.HasValue && l.QuantityReceived > 0)
            {
                var unitCost = poLine?.UnitPrice ?? 0m;
                await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                    entity.Id,
                    l.ItemId.Value,
                    l.QuantityReceived,
                    unitCost,
                    i.ReceiptDate,
                    grn.Number,
                    $"GRN {grn.Number} (PO {order.Number})"), ct);
            }
        }

        var allReceived = order.Lines.All(l => l.ReceivedQuantity >= l.Quantity);
        var anyReceived = order.Lines.Any(l => l.ReceivedQuantity > 0);

        if (allReceived && order.Status != PurchaseOrderStatus.Billed)
        {
            order.Status = PurchaseOrderStatus.Received;
        }
        else if (anyReceived && order.Status != PurchaseOrderStatus.Billed)
        {
            order.Status = PurchaseOrderStatus.PartiallyReceived;
        }

        await fields.ApplyAsync(Docs.GoodsReceipt, grn, i.CustomFields, isCreate: true, ct);

        receipts.Add(grn);
        await receipts.SaveChangesAsync(ct);
        await orders.SaveChangesAsync(ct);

        return await ToDtoAsync(grn, ct);
    }

    public async Task<Unit> Handle(DeleteGoodsReceipt cmd, CancellationToken ct)
    {
        var grn = await receipts.Tracked(new Specification<GoodsReceipt>(x => x.Id == cmd.Id))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException($"Goods receipt '{cmd.Id}' not found.");

        var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == grn.PurchaseOrderId))
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(ct);

        if (order != null)
        {
            foreach (var l in grn.Lines)
            {
                var poLine = order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId);
                if (poLine != null)
                {
                    poLine.ReceivedQuantity = Math.Max(0, poLine.ReceivedQuantity - l.QuantityReceived);
                }
            }
            if (order.Lines.All(l => l.ReceivedQuantity == 0) && order.Status != PurchaseOrderStatus.Billed)
            {
                order.Status = PurchaseOrderStatus.Approved;
            }
            else if (order.Lines.Any(l => l.ReceivedQuantity > 0) && order.Status != PurchaseOrderStatus.Billed)
            {
                order.Status = PurchaseOrderStatus.PartiallyReceived;
            }
            await orders.SaveChangesAsync(ct);
        }

        receipts.Remove(grn);
        await receipts.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private async Task<GoodsReceiptDto> ToDtoAsync(GoodsReceipt g, CancellationToken ct)
    {
        var cf = await fields.ReadAsync(Docs.GoodsReceipt, g, ct);
        return new GoodsReceiptDto(
            g.Id,
            g.Number,
            g.LegalEntityId,
            g.PurchaseOrderId,
            g.PurchaseOrderNumber,
            g.VendorName,
            g.ReceiptDate,
            g.ReceivedBy,
            g.DeliveryNoteNumber,
            g.Notes,
            g.Lines.Select(l => new GoodsReceiptLineDto(
                l.Id, l.PurchaseOrderLineId, l.ItemId, l.ItemCode, l.Description, l.QuantityReceived, l.UnitOfMeasure)).ToList(),
            cf,
            g.CreatedAt);
    }
}
