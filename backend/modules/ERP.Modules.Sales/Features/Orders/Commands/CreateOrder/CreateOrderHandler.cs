using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class CreateOrderHandler(
    ISalesOrderRepository orders,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    IReferenceCheck references) : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand cmd, CancellationToken ct)
    {
        // 1. The header, with a customer, items and quote of this workspace.
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        if (i.QuoteId is { } quoteId && !await references.AllExistAsync<SalesQuote>([quoteId], ct))
            throw new FieldValidationException("quoteId", "Quote not found.");
        var order = new SalesOrder
        {
            LegalEntityId = entity.Id,
            QuoteId = i.QuoteId,
            CustomerPartyId = i.CustomerPartyId,
            CustomerName = i.CustomerName.Trim(),
            CustomerReference = i.CustomerReference?.Trim(),
            OrderDate = i.OrderDate,
            ExpectedDeliveryDate = i.ExpectedDeliveryDate,
            Currency = i.Currency.Trim().ToUpperInvariant(),
            PaymentTerms = i.PaymentTerms,
            ShippingAddress = i.ShippingAddress,
            Notes = i.Notes,
            Status = SalesOrderStatus.Draft,
        };

        // 2. A free number (the next in the sequence unless one is given).
        order.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.SalesOrder, entity.Id, i.OrderDate, ct)
            : i.Number.Trim();

        if (await orders.AnyAsync(x => x.Number == order.Number, ct))
            throw new ConflictException($"Sales order number '{order.Number}' already exists.");

        // 3. Lines, totals and custom fields, then save.
        OrderLines.PopulateLinesAndTotals(order, i.Lines);
        await fields.ApplyAsync(Docs.SalesOrder, order, i.CustomFields, isCreate: true, ct);

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return await OrderDto.FromAsync(order, fields, ct);
    }
}
