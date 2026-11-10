using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Creates a party (customer, vendor...) under the next party number.</summary>
public sealed record CreatePartyCommand(PartyInput Input) : ICommand<PartyDto>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.PartiesWrite;
}
