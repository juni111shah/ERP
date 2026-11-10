using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using Unit = ERP.Modules.RealEstate.Domain.Unit; // not MediatR.Unit

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetUnitHandler(IReadRepository<Unit> units, ICustomFieldService fields, IClock clock) : IQueryHandler<GetUnitQuery, UnitDto>
{
    private DateOnly Today => DateOnly.FromDateTime(clock.UtcNow);

    public async Task<UnitDto> Handle(GetUnitQuery q, CancellationToken ct)
    {
        var unit = await units.GetAsync(q.Id, ct);
        return UnitDto.From(unit, Today, await fields.ReadAsync(Docs.Unit, unit, ct));
    }
}
