using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Finance;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class UpdateOrderFulfillmentHandler(
    ISalesOrderRepository orders,
    ICustomFieldService fields,
    IStockPostingService stockPosting,
    IUnitOfWork uow) : ICommandHandler<UpdateOrderFulfillmentCommand, OrderDto>
{
    // Stock issues, status and totals commit together: a failure part-way leaves no half-fulfilled order.
    public Task<OrderDto> Handle(UpdateOrderFulfillmentCommand cmd, CancellationToken ct) => uow.InTransactionAsync(token => FulfillAsync(cmd, token), ct);

    private async Task<OrderDto> FulfillAsync(UpdateOrderFulfillmentCommand cmd, CancellationToken ct)
    {
        // 1. Only a confirmed or processing order can be delivered.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.Status != SalesOrderStatus.Confirmed && order.Status != SalesOrderStatus.Processing)
            throw new ConflictException($"Cannot fulfill sales order in status '{order.Status}'. Only Confirmed or Processing orders can be fulfilled.");

        // 2. Each line moves forward only, never past what was ordered, and issues the stock delivered since last time.
        var deliveryDate = cmd.DeliveryDate ?? DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow);
        foreach (var ful in cmd.Lines)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == ful.LineId)
                ?? throw new FieldValidationException("lines", $"Line '{ful.LineId}' is not on sales order '{order.Number}'.");
            if (ful.DeliveredQuantity < line.DeliveredQuantity)
                throw new FieldValidationException("deliveredQuantity", $"Delivered quantity ({ful.DeliveredQuantity}) cannot be less than current delivered quantity ({line.DeliveredQuantity}).");
            if (ful.DeliveredQuantity > line.Quantity)
                throw new FieldValidationException("deliveredQuantity", $"Delivered quantity ({ful.DeliveredQuantity}) cannot be more than the {line.Quantity} ordered on '{line.Description}'.");

            var delta = ful.DeliveredQuantity - line.DeliveredQuantity;
            if (delta > 0 && line.ItemId.HasValue)
            {
                await stockPosting.IssueStockAsync(new StockIssueRequest(
                    order.LegalEntityId,
                    line.ItemId.Value,
                    delta,
                    deliveryDate,
                    order.Number,
                    $"Delivery for SO {order.Number} ({line.ItemCode})",
                    cmd.WarehouseId,
                    EnforceAvailable: true), ct);
            }
            line.DeliveredQuantity = ful.DeliveredQuantity;
        }

        // 3. The order's status follows what is delivered.
        var allDelivered = order.Lines.All(l => l.DeliveredQuantity >= l.Quantity);
        var anyDelivered = order.Lines.Any(l => l.DeliveredQuantity > 0);

        if (allDelivered && order.Status != SalesOrderStatus.Invoiced)
        {
            order.Status = SalesOrderStatus.Delivered;
        }
        else if (anyDelivered && order.Status == SalesOrderStatus.Confirmed)
        {
            order.Status = SalesOrderStatus.Processing;
        }

        await orders.SaveChangesAsync(ct);
        return await OrderDto.FromAsync(order, fields, ct);
    }
}
