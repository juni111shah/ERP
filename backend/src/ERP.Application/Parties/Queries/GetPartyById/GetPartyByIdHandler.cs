using ERP.Application.Common;
using ERP.Application.Customization;

namespace ERP.Application.Parties;

internal sealed class GetPartyByIdHandler(IPartyRepository parties, ICustomFieldService fields, PartyMapping mapping) : IQueryHandler<GetPartyByIdQuery, PartyDto>
{
    public async Task<PartyDto> Handle(GetPartyByIdQuery query, CancellationToken ct)
    {
        var p = await parties.GetAsync(query.Id, ct);
        return mapping.ToDto(p, await fields.ReadAsync(PartyMapping.EntityType, p, ct));
    }
}
