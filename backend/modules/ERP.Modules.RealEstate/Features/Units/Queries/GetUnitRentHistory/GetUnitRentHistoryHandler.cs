using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Repositories;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetUnitRentHistoryHandler(IReadRepository<Unit> units, IUnitRentRateRepository rates) : IQueryHandler<GetUnitRentHistoryQuery, IReadOnlyList<UnitRentRateDto>>
{
    public async Task<IReadOnlyList<UnitRentRateDto>> Handle(GetUnitRentHistoryQuery q, CancellationToken ct)
    {
        await units.GetAsync(q.UnitId, ct); // 404 for units outside the caller's scope
        return await rates.ListHistoryAsync(q.UnitId, ct);
    }
}
