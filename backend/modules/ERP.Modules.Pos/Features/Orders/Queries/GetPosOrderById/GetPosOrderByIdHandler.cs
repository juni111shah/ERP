using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosOrderByIdHandler(IPosOrderRepository orders, PosOrderRefundable refundable) : IQueryHandler<GetPosOrderByIdQuery, PosOrderDto>
{
    public async Task<PosOrderDto> Handle(GetPosOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await orders.FindWithLinesAndPaymentsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"Order {request.Id} not found");

        return await refundable.WithRefundableAsync(order, cancellationToken);
    }
}
