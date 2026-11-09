using ERP.Application.Common;
using ERP.Domain.Finance;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

/// <summary>What a sales return is raised against and the rules shared by create, post and void return.</summary>
public sealed class SalesReturnSources(ISalesOrderRepository orders, ISalesInvoiceRepository invoices)
{
    /// <summary>The order and invoice behind a return: naming one finds the other when the order was invoiced or the invoice came from an order.</summary>
    public async Task<(SalesOrder? Order, Invoice? Invoice)> SourceAsync(Guid? orderId, Guid? invoiceId, CancellationToken ct)
    {
        var order = orderId is { } oid
            ? await orders.FindWithLinesForUpdateAsync(oid, ct) ?? throw new FieldValidationException("salesOrderId", "Sales order not found.")
            : null;
        var linked = invoiceId ?? order?.InvoicedInvoiceId;
        var invoice = linked is { } iid
            ? await invoices.FindWithLinesAsync(iid, ct) ?? throw new FieldValidationException("invoiceId", "Invoice not found.")
            : null;
        if (invoice is not null)
        {
            if (invoice.IsCreditNote) throw new ConflictException("A credit note cannot be returned against.");
            if (invoice.Status is not (DocumentStatus.Issued or DocumentStatus.PartlyPaid or DocumentStatus.Paid))
                throw new ConflictException($"Invoice {invoice.Number} is not issued. Issue or delete it before recording a return.");
            if (order is null && invoice.SourceType == OrderInvoiceVoidedHandler.SourceType && invoice.SourceId is { } sourceOrder)
                order = await orders.FindWithLinesForUpdateAsync(sourceOrder, ct);
        }
        return (order, invoice);
    }

    /// <summary>Pairs an order line with the invoice line the conversion made from it (same item, description prefixed with the order number).</summary>
    public static (SalesOrderLine? OrderLine, InvoiceLine? InvoiceLine) Match(SalesOrder? order, Invoice? invoice, Guid? orderLineId, Guid? invoiceLineId)
    {
        static bool Same(SalesOrder o, SalesOrderLine ol, InvoiceLine il) => il.ItemId == ol.ItemId && il.Description == $"{o.Number}: {ol.Description}";
        if (orderLineId is { } olid)
        {
            var ol = order?.Lines.FirstOrDefault(x => x.Id == olid) ?? throw new FieldValidationException("lines", "A line does not belong to this sales order.");
            var il = invoice?.Lines.FirstOrDefault(x => Same(order!, ol, x));
            if (invoice is not null && il is null) throw new ConflictException($"'{ol.Description}' is not on invoice {invoice.Number}.");
            return (ol, il);
        }
        if (invoiceLineId is { } ilid)
        {
            var il = invoice?.Lines.FirstOrDefault(x => x.Id == ilid) ?? throw new FieldValidationException("lines", "A line does not belong to this invoice.");
            return (order?.Lines.FirstOrDefault(x => Same(order, x, il)), il);
        }
        throw new FieldValidationException("lines", "Each line needs the order line or invoice line it returns.");
    }

    public static void EnsureWithinDelivered(SalesOrderLine? line, decimal quantity)
    {
        if (line is null) return;
        var left = line.DeliveredQuantity - line.ReturnedQuantity;
        if (quantity > left) throw new FieldValidationException("lines", $"Only {left:0.####} of '{line.Description}' has been delivered and not returned; cannot return {quantity:0.####}.");
    }

    public static void RefreshStatus(SalesOrder order)
    {
        if (order.Status is SalesOrderStatus.Invoiced or SalesOrderStatus.Cancelled or SalesOrderStatus.Draft) return;
        order.Status = order.Lines.Count > 0 && order.Lines.All(l => l.DeliveredQuantity >= l.Quantity) ? SalesOrderStatus.Delivered
            : order.Lines.Any(l => l.DeliveredQuantity > 0) ? SalesOrderStatus.Processing
            : SalesOrderStatus.Confirmed;
    }
}
