using ERP.Application.Common;

namespace ERP.Modularity.Features;

/// <summary>Server-driven navigation: core items plus the groups of enabled modules, filtered by the caller's permissions.</summary>
public sealed record GetNavigationMenuQuery : IQuery<IReadOnlyList<MenuGroupDto>>;
