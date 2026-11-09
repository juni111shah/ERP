using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The people who can open the platform console.</summary>
public sealed record ListPlatformTeamQuery : IQuery<IReadOnlyList<PlatformTeamMember>>;
