using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetDealHandler(IReadRepository<BrokerageDeal> deals, ICustomFieldService fields) : IQueryHandler<GetDealQuery, DealDto>
{
    public async Task<DealDto> Handle(GetDealQuery q, CancellationToken ct)
    {
        var deal = await deals.GetAsync(q.Id, ct);
        return DealDto.From(deal, await fields.ReadAsync(Docs.Deal, deal, ct));
    }
}
