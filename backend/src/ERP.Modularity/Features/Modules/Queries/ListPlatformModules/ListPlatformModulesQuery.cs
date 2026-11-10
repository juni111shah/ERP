using ERP.Application.Common;

namespace ERP.Modularity.Features;

/// <summary>The installed modules a plan can include, by name (platform console and public price list).</summary>
public sealed record ListPlatformModulesQuery : IQuery<IReadOnlyList<PlatformModuleView>>;
