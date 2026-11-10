using ERP.Application.Common;
using ERP.Modules.Distribution.Repositories;

namespace ERP.Modules.Distribution.Features;

internal sealed class GetTripByIdHandler(IVanSalesTripRepository trips) : IQueryHandler<GetTripByIdQuery, VanSalesTripDto?>
{
    public async Task<VanSalesTripDto?> Handle(GetTripByIdQuery request, CancellationToken cancellationToken)
    {
        var trip = await trips.FindWithItemsAsync(request.Id, cancellationToken);

        return trip is null ? null : VanSalesTripDto.From(trip);
    }
}
