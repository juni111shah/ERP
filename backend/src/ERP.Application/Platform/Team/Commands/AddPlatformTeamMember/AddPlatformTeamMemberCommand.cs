using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Gives a person access to the platform console, creating their account with a temporary password when they have none.</summary>
public sealed record AddPlatformTeamMemberCommand(TeamMemberInput Input) : ICommand<NewTeamMember>;
