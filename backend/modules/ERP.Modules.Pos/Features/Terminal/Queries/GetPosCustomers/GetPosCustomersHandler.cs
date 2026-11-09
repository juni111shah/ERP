using ERP.Application.Common;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosCustomersHandler(IPosCustomerRepository parties) : IQueryHandler<GetPosCustomersQuery, List<PosCustomerDto>>
{
    public Task<List<PosCustomerDto>> Handle(GetPosCustomersQuery request, CancellationToken ct)
        => parties.SearchAsync(request.Search?.Trim(), Math.Clamp(request.Limit, 1, 50), ct);
}
