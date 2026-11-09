using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Creates a party group with a unique (upper-case) code.</summary>
public sealed record CreatePartyGroupCommand(PartyGroupInput Input) : ICommand<PartyGroupDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }
