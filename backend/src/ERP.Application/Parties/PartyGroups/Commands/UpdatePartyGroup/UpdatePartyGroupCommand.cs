using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Renames or re-codes a party group; the code stays unique.</summary>
public sealed record UpdatePartyGroupCommand(Guid Id, PartyGroupInput Input) : ICommand<PartyGroupDto>, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }
