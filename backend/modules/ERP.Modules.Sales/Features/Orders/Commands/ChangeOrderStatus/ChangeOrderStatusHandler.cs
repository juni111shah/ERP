using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class ChangeOrderStatusHandler(ISalesOrderRepository orders, ICustomFieldService fields, SalesOrderCredit credit) : ICommandHandler<ChangeOrderStatusCommand, OrderDto>
{
    public async Task<OrderDto> Handle(ChangeOrderStatusCommand cmd, CancellationToken ct)
    {
        // 1. The move must be allowed; an order with delivered goods cannot be cancelled.
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");

        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("An invoiced sales order is reopened by voiding its invoice.");
        SalesTransitions.Order.Ensure(order.Status, cmd.Status, isSystem: false);
        if (cmd.Status == SalesOrderStatus.Cancelled && order.Lines.Any(l => l.DeliveredQuantity > 0))
            throw new ConflictException("This sales order has delivered goods: stock has left and its cost is posted, so it cannot be cancelled. Record a sales return first.");

        // 2. Confirming checks the customer's credit (a warning, not a stop).
        var warning = cmd.Status == SalesOrderStatus.Confirmed ? await credit.CheckAsync(order, ct) : null;

        order.Status = cmd.Status;
        await orders.SaveChangesAsync(ct);
        return await OrderDto.FromAsync(order, fields, ct) with { Warnings = warning is null ? null : [warning] };
    }
}
