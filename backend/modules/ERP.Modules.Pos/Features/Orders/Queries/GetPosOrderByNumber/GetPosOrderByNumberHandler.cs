using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosOrderByNumberHandler(IPosOrderRepository orders, PosOrderRefundable refundable) : IQueryHandler<GetPosOrderByNumberQuery, PosOrderDto>
{
    public async Task<PosOrderDto> Handle(GetPosOrderByNumberQuery request, CancellationToken cancellationToken)
    {
        var number = request.Number.Trim();
        var order = await orders.FindByNumberWithLinesAndPaymentsAsync(number, cancellationToken)
            ?? throw new NotFoundException($"No sale with receipt number {number}.");

        return await refundable.WithRefundableAsync(order, cancellationToken);
    }
}
