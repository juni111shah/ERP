using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Parties;

/// <summary>Soft-deletes a group that no party uses.</summary>
public sealed record DeletePartyGroupCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.PartyGroupsWrite; }
