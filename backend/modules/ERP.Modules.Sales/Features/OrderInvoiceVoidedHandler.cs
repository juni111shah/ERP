using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Sales.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

/// <summary>When the invoice generated from a sales order is voided, the order is reopened so it can be invoiced again.</summary>
public sealed class OrderInvoiceVoidedHandler(IRepository<SalesOrder> orders) : INotificationHandler<InvoiceVoided>
{
    public const string SourceType = "salesorder";

    public async Task Handle(InvoiceVoided n, CancellationToken ct)
    {
        if (n.SourceType != SourceType || n.SourceId is not { } orderId) return;

        var order = await orders.Tracked(new Specification<SalesOrder>(o => o.Id == orderId))
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(ct);
        if (order is null || order.InvoicedInvoiceId != n.InvoiceId) return;

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
