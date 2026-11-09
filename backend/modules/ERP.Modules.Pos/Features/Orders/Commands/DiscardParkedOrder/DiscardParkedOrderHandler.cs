using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;
using MediatR;

namespace ERP.Modules.Pos.Features;

internal sealed class DiscardParkedOrderHandler(IPosOrderRepository orders) : ICommandHandler<DiscardParkedOrderCommand>
{
    public async Task<Unit> Handle(DiscardParkedOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await orders.FindForUpdateAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException($"Order {request.OrderId} not found");
        if (order.Status != PosOrderStatus.Parked) throw new ConflictException("Only a held sale can be discarded.");
        orders.Remove(order);
        await orders.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
