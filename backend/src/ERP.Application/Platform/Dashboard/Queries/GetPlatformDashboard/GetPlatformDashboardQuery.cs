using ERP.Application.Common;

namespace ERP.Application.Platform;

/// <summary>The founder's overview: recurring revenue, clients by state and plan, money owed and collected, trials ending and recent activity.</summary>
public sealed record GetPlatformDashboardQuery : IQuery<PlatformDashboard>;
