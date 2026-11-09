using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;
using MediatR;

namespace ERP.Modules.Purchasing.Features;

internal sealed class DeleteGoodsReceiptHandler(
    IGoodsReceiptRepository receipts,
    IPurchaseOrderRepository orders,
    IStockPostingService stockPosting,
    IUnitOfWork unitOfWork,
    IReadRepository<Item> items) : ICommandHandler<DeleteGoodsReceiptCommand>
{
    public async Task<Unit> Handle(DeleteGoodsReceiptCommand cmd, CancellationToken ct)
    {
        return await unitOfWork.InTransactionAsync(async token =>
        {
            // 1. Billed quantity cannot be un-received.
            var grn = await receipts.FindWithLinesForUpdateAsync(cmd.Id, token) ?? throw new NotFoundException($"Goods receipt '{cmd.Id}' not found.");

            var order = await orders.FindWithLinesForUpdateAsync(grn.PurchaseOrderId, token);

            if (order != null && (order.Status == PurchaseOrderStatus.Billed
                || grn.Lines.Any(l => order.Lines.FirstOrDefault(x => x.Id == l.PurchaseOrderLineId) is { } line && line.ReceivedQuantity - l.QuantityReceived < line.BilledQuantity)))
            {
                throw new ConflictException("Cannot delete a goods receipt for quantity that has already been billed. Void or delete the bill first.");
            }

            // 2. Take the quantities off the order.
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
                await PurchaseOrderProgress.RefreshAsync(order, items, stockPosting, token);
                await orders.SaveChangesAsync(token);
            }

            // 3. Reverse the stock and delete the receipt.
            await stockPosting.ReverseReceiptAsync(grn.Number, token);

            receipts.Remove(grn);
            await receipts.SaveChangesAsync(token);
            return Unit.Value;
        }, ct);
    }
}
