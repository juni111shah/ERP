namespace ERP.Modularity.Features;

public sealed record ModuleView(
    string Key, string Name, string Description, string Category, string Version, bool IsCore, IReadOnlyList<string> Requires, string UiRoute, string State);

public sealed record ModuleState(string Key, string State);

/// <summary>The modules a plan can include (platform console).</summary>
public sealed record PlatformModuleView(string Key, string Name, string Description, string Category, bool IsCore, IReadOnlyList<string> Requires);
