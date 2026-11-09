using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class UpdateOrderHandler(
    ISalesOrderRepository orders,
    ICustomFieldService fields,
    IReferenceCheck references,
    SalesOrderCredit credit) : ICommandHandler<UpdateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(UpdateOrderCommand cmd, CancellationToken ct)
    {
        // 1. Only a draft order, or a confirmed one with nothing delivered, can change.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        var canEdit = order.Status == SalesOrderStatus.Draft ||
                      (order.Status == SalesOrderStatus.Confirmed && order.Lines.All(l => l.DeliveredQuantity == 0));
        if (!canEdit)
            throw new ConflictException($"Cannot edit sales order in status '{order.Status}'" + (order.Lines.Any(l => l.DeliveredQuantity > 0) ? " with delivered quantities." : "."));

        // 2. The header, with a customer and items of this workspace.
        var i = cmd.Input;
        await SalesReferences.EnsureAsync(references, i.CustomerPartyId, i.Lines.Select(l => l.ItemId), ct);
        order.CustomerPartyId = i.CustomerPartyId;
        order.CustomerName = i.CustomerName.Trim();
        order.CustomerReference = i.CustomerReference?.Trim();
        order.OrderDate = i.OrderDate;
        order.ExpectedDeliveryDate = i.ExpectedDeliveryDate;
        order.Currency = i.Currency.Trim().ToUpperInvariant();
        order.PaymentTerms = i.PaymentTerms;
        order.ShippingAddress = i.ShippingAddress;
        order.Notes = i.Notes;

        // 3. Lines, totals and custom fields; a confirmed order can grow past the credit limit, so it is checked again.
        OrderLines.PopulateLinesAndTotals(order, i.Lines);
        await fields.ApplyAsync(Docs.SalesOrder, order, i.CustomFields, isCreate: false, ct);
        var warning = order.Status == SalesOrderStatus.Confirmed ? await credit.CheckAsync(order, ct) : null; // a confirmed order can grow past the limit

        await orders.SaveChangesAsync(ct);
        return await OrderDto.FromAsync(order, fields, ct) with { Warnings = warning is null ? null : [warning] };
    }
}
