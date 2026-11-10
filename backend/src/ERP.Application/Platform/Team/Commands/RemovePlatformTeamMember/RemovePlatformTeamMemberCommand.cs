using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Takes away a person's access to the platform console.</summary>
public sealed record RemovePlatformTeamMemberCommand(Guid UserId) : ICommand;
