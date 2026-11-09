using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Modules.Purchasing.Domain;
using ERP.Modules.Purchasing.Repositories;

namespace ERP.Modules.Purchasing.Features;

/// <summary>What a return is raised against, and how much of it can go back. Shared by create and post return.</summary>
public sealed class PurchaseReturnSources(IPurchaseOrderRepository orders, IPurchasingBillRepository bills, IPurchaseOrderBillLineRepository billLinks)
{
    /// <summary>The order and bill behind a return: a bill raised from an order brings the order with it.</summary>
    public async Task<(PurchaseOrder? Order, Bill? Bill)> SourceAsync(Guid? orderId, Guid? billId, CancellationToken ct)
    {
        var bill = billId is { } bid
            ? await bills.FindWithLinesForUpdateAsync(bid, ct) ?? throw new FieldValidationException("billId", "Bill not found.")
            : null;
        if (bill is not null && bill.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid))
            throw new ConflictException($"Bill {bill.Number} is not posted. Post or delete it before recording a return.");
        orderId ??= bill is null ? null : await billLinks.FindOrderIdOfBillAsync(bill.Id, ct);
        var order = orderId is { } oid
            ? await orders.FindWithLinesForUpdateAsync(oid, ct) ?? throw new FieldValidationException("purchaseOrderId", "Purchase order not found.")
            : null;
        return (order, bill);
    }

    public static void EnsureWithinReceived(PurchaseOrderLine line, decimal quantity)
    {
        if (quantity > line.ReceivedQuantity)
            throw new FieldValidationException("lines", $"Only {line.ReceivedQuantity:0.####} of '{line.Description}' has been received; cannot return {quantity:0.####}.");
    }
}
