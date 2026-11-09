using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class ConvertQuoteToOrderHandler(
    ISalesQuoteRepository quotes,
    ISalesOrderRepository orders,
    INumberingService numbers,
    ICustomFieldService fields,
    SalesOrderCredit credit) : ICommandHandler<ConvertQuoteToOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(ConvertQuoteToOrderCommand cmd, CancellationToken ct)
    {
        // 1. A quote already converted returns its order.
        var quote = await quotes.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Quote '{cmd.Id}' not found.");

        if (quote.Status == SalesQuoteStatus.Converted && quote.ConvertedSalesOrderId.HasValue)
        {
            var existingOrder = await orders.FindWithLinesForUpdateAsync(quote.ConvertedSalesOrderId.Value, ct);
            if (existingOrder != null) return await OrderDto.FromAsync(existingOrder, fields, ct);
        }

        SalesTransitions.Quote.Ensure(quote.Status, SalesQuoteStatus.Converted, isSystem: true);

        // 2. A confirmed order with the quote's lines and totals.
        var order = new SalesOrder
        {
            LegalEntityId = quote.LegalEntityId,
            QuoteId = quote.Id,
            QuoteNumber = quote.Number,
            CustomerPartyId = quote.CustomerPartyId,
            CustomerName = quote.CustomerName,
            OrderDate = DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow),
            Currency = quote.Currency,
            Status = SalesOrderStatus.Confirmed,
            PaymentTerms = quote.Terms,
            Notes = quote.Notes,
            Number = await numbers.NextAsync(Docs.SalesOrder, quote.LegalEntityId, DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow), ct),
        };

        foreach (var l in quote.Lines)
        {
            order.Lines.Add(new SalesOrderLine
            {
                ItemId = l.ItemId,
                ItemCode = l.ItemCode,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitOfMeasure = l.UnitOfMeasure,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                TaxRate = l.TaxRate,
                LineTotal = l.LineTotal,
            });
        }

        order.SubTotal = quote.SubTotal;
        order.TaxTotal = quote.TaxTotal;
        order.DiscountTotal = quote.DiscountTotal;
        order.TotalAmount = quote.TotalAmount;
        var warning = await credit.CheckAsync(order, ct); // a converted order starts confirmed

        // 3. Save the order and mark the quote converted.
        orders.Add(order);
        quote.Status = SalesQuoteStatus.Converted;
        quote.ConvertedSalesOrderId = order.Id;

        await quotes.SaveChangesAsync(ct);
        await orders.SaveChangesAsync(ct);

        return await OrderDto.FromAsync(order, fields, ct) with { Warnings = warning is null ? null : [warning] };
    }
}
