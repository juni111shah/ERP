using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Purchasing.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Purchasing.Features;

/// <summary>When a bill generated from a purchase order is deleted or voided, the quantities it covered can be billed again and the order goes back to what it has received.</summary>
public sealed class OrderBillReleasedHandler(IRepository<PurchaseOrder> orders, IRepository<PurchaseOrderBillLine> billLines, IReadRepository<Item> items, IStockPostingService stock) : INotificationHandler<BillReleased>
{
    public async Task Handle(BillReleased n, CancellationToken ct)
    {
        var rows = await billLines.Tracked(new Specification<PurchaseOrderBillLine>(r => r.BillId == n.BillId)).ToListAsync(ct);
        var orderId = rows.Count > 0 ? rows[0].OrderId : (Guid?)null;
        var order = await orders.Tracked(new Specification<PurchaseOrder>(o => orderId != null ? o.Id == orderId : o.InvoicedBillId == n.BillId))
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(ct);
        if (order is null) return;

        // A bill from before bills were linked line by line covered the whole order.
        if (rows.Count == 0)
            foreach (var l in order.Lines) l.BilledQuantity = 0;
        foreach (var r in rows)
        {
            if (order.Lines.FirstOrDefault(l => l.Id == r.OrderLineId) is { } line) line.BilledQuantity = Math.Max(0, line.BilledQuantity - r.Quantity);
            billLines.Remove(r);
        }

        if (order.InvoicedBillId == n.BillId)
            order.InvoicedBillId = await billLines.Query(new Specification<PurchaseOrderBillLine>(r => r.OrderId == order.Id && r.BillId != n.BillId))
                .OrderByDescending(r => r.CreatedAt).Select(r => (Guid?)r.BillId).FirstOrDefaultAsync(ct);
        await PurchaseOrderProgress.RefreshAsync(order, items, stock, ct);
        await orders.SaveChangesAsync(ct);
    }
}

/// <summary>A draft bill can be edited; it still cannot clear more goods-received value than the quantity it was raised for.</summary>
public sealed class OrderBillPostingHandler(IReadRepository<PurchaseOrderBillLine> billLines, IReadRepository<Account> accounts) : INotificationHandler<BillPosting>
{
    public async Task Handle(BillPosting n, CancellationToken ct)
    {
        var rows = await billLines.Query(new Specification<PurchaseOrderBillLine>(r => r.BillId == n.BillId)).Select(r => r.ClearingQuantity).ToListAsync(ct);
        if (rows.Count == 0) return;
        var clearing = await accounts.Query(new Specification<Account>(a => a.SystemRole == SystemRoles.InventoryClearing)).Select(a => a.Id).ToListAsync(ct);
        var onBill = n.Lines.Where(l => l.AccountId is { } id && clearing.Contains(id)).Sum(l => l.Quantity);
        if (onBill > rows.Sum())
            throw new ConflictException($"This bill clears {onBill:0.####} of goods but its purchase order only allowed {rows.Sum():0.####}. Raise another bill from the purchase order for the rest.");
    }
}
