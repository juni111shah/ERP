using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class GetRouteByIdHandler(IDistributionRouteRepository routes) : IQueryHandler<GetRouteByIdQuery, DistributionRouteDto?>
{
    public async Task<DistributionRouteDto?> Handle(GetRouteByIdQuery request, CancellationToken cancellationToken)
    {
        var route = await routes.FindWithStopsAsync(request.Id, cancellationToken);

        return route is null ? null : DistributionRouteDto.From(route);
    }
}
