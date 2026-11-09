using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

internal sealed class GetLeadHandler(IReadRepository<Lead> leads) : IQueryHandler<GetLeadQuery, LeadDto>
{
    public async Task<LeadDto> Handle(GetLeadQuery q, CancellationToken ct) => LeadDto.From(await leads.GetAsync(q.Id, ct));
}
