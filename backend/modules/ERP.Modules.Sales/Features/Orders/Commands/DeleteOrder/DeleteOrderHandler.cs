using ERP.Application.Common;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;
using MediatR;

namespace ERP.Modules.Sales.Features;

internal sealed class DeleteOrderHandler(ISalesOrderRepository orders) : ICommandHandler<DeleteOrderCommand>
{
    public async Task<Unit> Handle(DeleteOrderCommand cmd, CancellationToken ct)
    {
        var order = await orders.FindWithLinesForUpdateAsync(cmd.Id, ct) ?? throw new NotFoundException($"Sales order '{cmd.Id}' not found.");
        if (order.Status == SalesOrderStatus.Invoiced)
            throw new ConflictException("Cannot delete an invoiced sales order.");
        if (order.Lines.Any(l => l.DeliveredQuantity > 0))
            throw new ConflictException("This sales order has delivered goods: stock has left and its cost is posted, so it cannot be deleted. Record a sales return first.");

        orders.Remove(order);
        await orders.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
