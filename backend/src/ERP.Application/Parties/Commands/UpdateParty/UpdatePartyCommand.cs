using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Updates a party; masked sensitive values are kept when the caller may not see them.</summary>
public sealed record UpdatePartyCommand(Guid Id, PartyInput Input) : ICommand<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesWrite;
}
