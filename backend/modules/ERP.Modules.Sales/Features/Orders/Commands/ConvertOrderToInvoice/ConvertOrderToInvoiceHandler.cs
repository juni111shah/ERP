using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Organizations;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class ConvertOrderToInvoiceHandler(
    ISalesOrderRepository orders,
    ISalesTaxCodeRepository taxCodes,
    ISender sender,
    ILegalEntityGuard guard,
    IStockPostingService stockPosting,
    IUnitOfWork uow) : ICommandHandler<ConvertOrderToInvoiceCommand, InvoiceDto>
{
    // Invoice, auto-fulfilment stock issue and order status commit together.
    public Task<InvoiceDto> Handle(ConvertOrderToInvoiceCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => ConvertAsync(cmd, token), ct);

    private async Task<InvoiceDto> ConvertAsync(ConvertOrderToInvoiceCommand cmd, CancellationToken ct)
    {
        // 1. An order already invoiced returns its invoice.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.InvoicedInvoiceId.HasValue)
        {
            return await sender.Send(new GetInvoiceByIdQuery(order.InvoicedInvoiceId.Value), ct);
        }

        // 2. The order needs a customer and the legal entity's currency.
        SalesTransitions.Order.Ensure(order.Status, SalesOrderStatus.Invoiced, isSystem: true);

        if (!order.CustomerPartyId.HasValue)
        {
            throw new ConflictException("Sales order must have an assigned Customer Party to generate an accounting invoice.");
        }

        var entity = await guard.RequireAsync(order.LegalEntityId, ct);
        if (!string.Equals(order.Currency, entity.BaseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("Foreign-currency sales orders cannot be invoiced yet.");
        }

        // 3. Each line gets the tax code with its rate (the legal entity's own first, then a shared one).
        var activeTaxCodes = await taxCodes.ListActiveForLegalEntityAsync(order.LegalEntityId, ct);

        var invoiceLines = new List<InvoiceLineInput>();
        foreach (var l in order.Lines)
        {
            Guid? taxCodeId = null;
            if (l.TaxRate > 0)
            {
                var matchedCode = activeTaxCodes.FirstOrDefault(t => t.LegalEntityId == order.LegalEntityId && t.Rate == l.TaxRate)
                    ?? activeTaxCodes.FirstOrDefault(t => t.LegalEntityId == null && t.Rate == l.TaxRate);

                if (matchedCode is null)
                {
                    throw new ConflictException($"No active tax code found for line '{l.Description}' with rate {l.TaxRate}%.");
                }
                taxCodeId = matchedCode.Id;
            }

            invoiceLines.Add(new InvoiceLineInput(
                Description: $"{order.Number}: {l.Description}",
                Quantity: l.Quantity,
                Price: Math.Round(l.UnitPrice * (1m - (l.DiscountPercent / 100m)), 4),
                TaxCodeId: taxCodeId,
                ItemId: l.ItemId
            ));
        }

        // 4. Raise the invoice; it must come to the order's total.
        var invoice = await sender.Send(new CreateInvoiceCommand(new InvoiceInput(
            LegalEntityId: order.LegalEntityId,
            PartyId: order.CustomerPartyId.Value,
            InvoiceDate: DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
            DueDate: null,
            Lines: invoiceLines,
            Notes: $"Generated from Sales Order {order.Number}",
            SourceType: "salesorder",
            SourceId: order.Id
        )), ct);

        var maxAllowedDiff = 0.01m * Math.Max(1, order.Lines.Count);
        if (Math.Abs(invoice.Total - order.TotalAmount) > maxAllowedDiff)
        {
            await sender.Send(new DeleteInvoiceCommand(invoice.Id), ct);
            throw new ConflictException($"Invoice total {invoice.Total} does not match sales order total {order.TotalAmount}.");
        }

        // 5. Deliver what was not delivered yet and mark everything invoiced.
        foreach (var l in order.Lines)
        {
            var undelivered = l.Quantity - l.DeliveredQuantity;
            if (undelivered > 0 && l.ItemId.HasValue)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    l.ItemId.Value,
                    undelivered,
                    DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
                    order.Number,
                    $"Auto-fulfillment for Invoiced SO {order.Number} ({l.ItemCode})",
                    EnforceAvailable: true), ct);
                l.AutoDeliveredQuantity = undelivered;
                l.DeliveredQuantity = l.Quantity;
            }
            l.InvoicedQuantity = l.Quantity;
        }

        order.Status = SalesOrderStatus.Invoiced;
        order.InvoicedInvoiceId = invoice.Id;
        await orders.SaveChangesAsync(ct);

        return invoice;
    }
}
