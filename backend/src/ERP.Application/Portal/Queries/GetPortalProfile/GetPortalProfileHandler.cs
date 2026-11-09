using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Parties;

namespace ERP.Application.Portal;

internal sealed class GetPortalProfileHandler(IReadRepository<Party> parties, ICurrentUser user) : IQueryHandler<GetPortalProfileQuery, PortalProfileDto>
{
    public async Task<PortalProfileDto> Handle(GetPortalProfileQuery query, CancellationToken ct)
    {
        var p = await parties.GetAsync(PortalCaller.AnyParty(user), ct);
        return new(p.Id, p.Name, p.Email, p.Phone);
    }
}
