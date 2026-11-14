using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>Makes a platform team member a manager, or support again. Owner only (the platform-owner policy on the endpoint).</summary>
public sealed record SetPlatformTeamRoleCommand(Guid UserId, PlatformTeamRole Role) : ICommand<PlatformTeamMember>;
