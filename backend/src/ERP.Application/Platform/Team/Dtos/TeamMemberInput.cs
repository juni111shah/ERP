namespace ERP.Application.Platform;

/// <summary>A new platform team member. Only the owner may add someone as a manager; everyone else adds support members.</summary>
public sealed record TeamMemberInput(string Email, string FullName, PlatformTeamRole Role = PlatformTeamRole.Support);

/// <summary>A team member's new role: support or manager.</summary>
public sealed record TeamRoleInput(PlatformTeamRole Role);
