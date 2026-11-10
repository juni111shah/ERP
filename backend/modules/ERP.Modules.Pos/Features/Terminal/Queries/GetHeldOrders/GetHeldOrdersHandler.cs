using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetHeldOrdersHandler(IPosOrderRepository orders) : IQueryHandler<GetHeldOrdersQuery, List<PosOrderDto>>
{
    public async Task<List<PosOrderDto>> Handle(GetHeldOrdersQuery request, CancellationToken ct)
    {
        var held = await orders.ListParkedAsync(request.RegisterId, ct);
        return held.Select(PosOrderDto.From).ToList();
    }
}
