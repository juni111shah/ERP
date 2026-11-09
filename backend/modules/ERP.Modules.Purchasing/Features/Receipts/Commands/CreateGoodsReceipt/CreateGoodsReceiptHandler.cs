using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Domain.Catalog;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

internal sealed class CreateGoodsReceiptHandler(
    IGoodsReceiptRepository receipts,
    IPurchaseOrderRepository orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IStockPostingService stockPosting,
    ISettingsService settings,
    IUnitOfWork unitOfWork,
    IReadRepository<Item> items) : ICommandHandler<CreateGoodsReceiptCommand, GoodsReceiptDto>
{
    public async Task<GoodsReceiptDto> Handle(CreateGoodsReceiptCommand cmd, CancellationToken ct)
    {
        // 1. An approved order of the same legal entity, a usable warehouse, and lines for the order's own items.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        var order = await orders.FindWithLinesForUpdateAsync(i.PurchaseOrderId, ct) ?? throw new NotFoundException($"Purchase order '{i.PurchaseOrderId}' not found.");

        if (order.Status == PurchaseOrderStatus.Billed)
        {
            throw new ConflictException("Cannot create a goods receipt for an already billed purchase order.");
        }

        if (order.Status is not (PurchaseOrderStatus.Approved or PurchaseOrderStatus.PartiallyReceived))
            throw new ConflictException("Only an approved purchase order can be received.");
        if (order.LegalEntityId != i.LegalEntityId)
            throw new FieldValidationException("legalEntityId", "The goods receipt must be for the legal entity of the purchase order.");
        if (i.WarehouseId is { } warehouseId && !await stockPosting.IsWarehouseUsableAsync(warehouseId, entity.Id, ct))
            throw new FieldValidationException("warehouseId", "The warehouse was not found, is not active, or belongs to another legal entity.");
        foreach (var l in i.Lines)
        {
            var match = order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId);
            if (match is not null && l.ItemId is not null && l.ItemId != match.ItemId)
                throw new FieldValidationException("lines", $"Line '{match.Description}' is for a different item than the purchase order line.");
        }

        // 2. No line may receive more than is left plus the tolerance.
        decimal tolerancePct = 0m;
        try
        {
            var raw = await settings.GetRawAsync("purchasing.receiptTolerancePercent", ct: ct);
            if (!string.IsNullOrWhiteSpace(raw) && decimal.TryParse(raw.Trim('"'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                tolerancePct = Math.Max(0m, parsed);
        }
        catch { tolerancePct = 0m; }

        // Lines for the same order line count together, so splitting a quantity over several lines cannot get past the limit.
        foreach (var group in i.Lines.GroupBy(x => x.PurchaseOrderLineId))
        {
            var poLine = order.Lines.FirstOrDefault(x => x.Id == group.Key)
                ?? throw new FieldValidationException("lines", $"Purchase order line '{group.Key}' not found on order '{order.Number}'.");

            if (group.Any(l => l.QuantityReceived <= 0))
                throw new FieldValidationException("lines", "Received quantity must be greater than zero.");

            var requested = group.Sum(l => l.QuantityReceived);
            var remaining = poLine.Quantity - poLine.ReceivedQuantity;
            var toleranceQty = poLine.Quantity * (tolerancePct / 100m);
            var maxAllowed = remaining + toleranceQty;

            if (requested > maxAllowed)
            {
                throw new FieldValidationException("lines",
                    $"Received quantity {requested} exceeds the maximum allowable quantity {maxAllowed:0.####} (ordered: {poLine.Quantity:0.####}, already received: {poLine.ReceivedQuantity:0.####}, tolerance: {tolerancePct}%).");
            }
        }

        // 3. Receive the stock and save the receipt and the order together.
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
                WarehouseId = i.WarehouseId,
            };

            grn.Number = string.IsNullOrWhiteSpace(i.Number)
                ? await numbers.NextAsync(Docs.GoodsReceipt, entity.Id, i.ReceiptDate, token)
                : i.Number.Trim();

            foreach (var l in i.Lines)
            {
                var poLine = order.Lines.First(x => x.Id == l.PurchaseOrderLineId); // validated above
                poLine.ReceivedQuantity += l.QuantityReceived;

                Guid? moved = null;
                if (poLine.ItemId.HasValue && l.QuantityReceived > 0)
                {
                    var unitCost = Math.Round(poLine.UnitPrice * (1m - (poLine.DiscountPercent / 100m)), 4);
                    moved = await stockPosting.ReceiveStockAsync(new StockReceiveRequest(
                        entity.Id,
                        poLine.ItemId.Value,
                        l.QuantityReceived,
                        unitCost,
                        i.ReceiptDate,
                        grn.Number,
                        $"GRN {grn.Number} (PO {order.Number})",
                        i.WarehouseId,
                        RequireWarehouse: true), token);
                }

                // Item, code, description and unit come from the order line, never from the request.
                grn.Lines.Add(new GoodsReceiptLine
                {
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    ItemId = poLine.ItemId,
                    ItemCode = poLine.ItemCode,
                    Description = poLine.Description,
                    QuantityReceived = l.QuantityReceived,
                    UnitOfMeasure = string.IsNullOrWhiteSpace(poLine.UnitOfMeasure) ? "EA" : poLine.UnitOfMeasure,
                    NoStockMoved = moved is null,
                });
            }

            await PurchaseOrderProgress.RefreshAsync(order, items, stockPosting, token);

            await fields.ApplyAsync(Docs.GoodsReceipt, grn, i.CustomFields, isCreate: true, token);

            receipts.Add(grn);
            await receipts.SaveChangesAsync(token);
            await orders.SaveChangesAsync(token);

            return await GoodsReceiptDto.FromAsync(grn, fields, token);
        }, ct);
    }
}
