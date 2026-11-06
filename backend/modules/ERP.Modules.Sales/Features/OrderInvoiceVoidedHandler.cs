using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Sales.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

/// <summary>
/// When the invoice generated from a sales order is voided or its draft deleted, the order is reopened so it can be invoiced again,
/// and what the conversion itself issued goes back on the shelf at the cost it left at, so no cost stays posted with no revenue against it.
/// Goods delivered before the conversion stay delivered.
/// </summary>
public sealed class OrderInvoiceVoidedHandler(IRepository<SalesOrder> orders, IStockPostingService stock) : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "salesorder";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } orderId) return;

        var order = await orders.Tracked(new Specification<SalesOrder>(o => o.Id == orderId))
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(ct);
        if (order is null || order.InvoicedInvoiceId != n.InvoiceId) return;

        foreach (var l in order.Lines.Where(l => l.AutoDeliveredQuantity > 0))
        {
            if (l.ItemId is { } itemId)
                await stock.ReturnIssuedStockAsync(order.Number, itemId, l.AutoDeliveredQuantity, $"{order.Number}-REV", DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), ct);
            l.DeliveredQuantity -= l.AutoDeliveredQuantity;
            l.AutoDeliveredQuantity = 0;
        }

        var target = order.Lines.Count > 0 && order.Lines.All(l => l.DeliveredQuantity >= l.Quantity) ? SalesOrderStatus.Delivered
            : order.Lines.Any(l => l.DeliveredQuantity > 0) ? SalesOrderStatus.Processing
            : SalesOrderStatus.Confirmed;
        SalesTransitions.Order.Ensure(order.Status, target, isSystem: true);

        order.Status = target;
        order.InvoicedInvoiceId = null;
        foreach (var l in order.Lines) l.InvoicedQuantity = 0;
        await orders.SaveChangesAsync(ct);
    }
}
