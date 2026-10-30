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
    IStockPostingService stockPosting,
    ISettingsService settings,
    IUnitOfWork unitOfWork) :
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

        if (order.Status == PurchaseOrderStatus.Billed || order.InvoicedBillId.HasValue)
        {
            throw new ConflictException("Cannot create a goods receipt for an already billed purchase order.");
        }

        decimal tolerancePct = 0m;
        try
        {
            var raw = await settings.GetRawAsync("purchasing.receiptTolerancePercent", ct: ct);
            if (!string.IsNullOrWhiteSpace(raw) && decimal.TryParse(raw.Trim('"'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                tolerancePct = Math.Max(0m, parsed);
        }
        catch { tolerancePct = 0m; }

        foreach (var l in i.Lines)
        {
            var poLine = order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId)
                ?? throw new FieldValidationException("lines", $"Purchase order line '{l.PurchaseOrderLineId}' not found on order '{order.Number}'.");

            if (l.QuantityReceived <= 0)
                throw new FieldValidationException("lines", "Received quantity must be greater than zero.");

            var remaining = poLine.Quantity - poLine.ReceivedQuantity;
            var toleranceQty = poLine.Quantity * (tolerancePct / 100m);
            var maxAllowed = remaining + toleranceQty;

            if (l.QuantityReceived > maxAllowed)
            {
                throw new FieldValidationException("lines",
                    $"Received quantity {l.QuantityReceived} exceeds the maximum allowable quantity {maxAllowed:0.####} (ordered: {poLine.Quantity:0.####}, already received: {poLine.ReceivedQuantity:0.####}, tolerance: {tolerancePct}%).");
            }
        }

        return await unitOfWork.InTransactionAsync(async token =>
        {
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
                ? await numbers.NextAsync(Docs.GoodsReceipt, entity.Id, i.ReceiptDate, token)
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
                    var unitCost = poLine != null ? Math.Round(poLine.UnitPrice * (1m - (poLine.DiscountPercent / 100m)), 4) : 0m;
                    await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                        entity.Id,
                        l.ItemId.Value,
                        l.QuantityReceived,
                        unitCost,
                        i.ReceiptDate,
                        grn.Number,
                        $"GRN {grn.Number} (PO {order.Number})"), token);
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

            await fields.ApplyAsync(Docs.GoodsReceipt, grn, i.CustomFields, isCreate: true, token);

            receipts.Add(grn);
            await receipts.SaveChangesAsync(token);
            await orders.SaveChangesAsync(token);

            return await ToDtoAsync(grn, token);
        }, ct);
    }

    public async Task<Unit> Handle(DeleteGoodsReceipt cmd, CancellationToken ct)
    {
        return await unitOfWork.InTransactionAsync(async token =>
        {
            var grn = await receipts.Tracked(new Specification<GoodsReceipt>(x => x.Id == cmd.Id))
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(token) ?? throw new NotFoundException($"Goods receipt '{cmd.Id}' not found.");

            var order = await orders.Tracked(new Specification<PurchaseOrder>(x => x.Id == grn.PurchaseOrderId))
                .Include(x => x.Lines)
                .FirstOrDefaultAsync(token);

            if (order != null && (order.Status == PurchaseOrderStatus.Billed || order.InvoicedBillId.HasValue))
            {
                throw new ConflictException("Cannot delete a goods receipt for an already billed purchase order.");
            }

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
                await orders.SaveChangesAsync(token);
            }

            await stockPosting.ReverseReceiptAsync(grn.Number, token);

            receipts.Remove(grn);
            await receipts.SaveChangesAsync(token);
            return Unit.Value;
        }, ct);
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
