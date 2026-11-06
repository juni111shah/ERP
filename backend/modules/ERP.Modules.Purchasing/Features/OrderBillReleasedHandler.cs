using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Purchasing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>When the bill generated from a purchase order is deleted or voided, the order goes back to what it has received so it can be billed again.</summary>
public sealed class OrderBillReleasedHandler(IRepository<PurchaseOrder> orders) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var order = await orders.Tracked(new Specification<PurchaseOrder>(o => o.InvoicedBillId == n.BillId))
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(ct);
        if (order is null) return;

        order.Status = order.Lines.Count > 0 && order.Lines.All(l => l.ReceivedQuantity >= l.Quantity) ? PurchaseOrderStatus.Received
            : order.Lines.Any(l => l.ReceivedQuantity > 0) ? PurchaseOrderStatus.PartiallyReceived
            : PurchaseOrderStatus.Approved;
        order.InvoicedBillId = null;
        foreach (var l in order.Lines) l.BilledQuantity = 0;
        await orders.SaveChangesAsync(ct);
    }
}
