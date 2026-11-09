using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>One party with its custom fields.</summary>
public sealed record GetPartyByIdQuery(Guid Id) : IQuery<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesRead;
}
