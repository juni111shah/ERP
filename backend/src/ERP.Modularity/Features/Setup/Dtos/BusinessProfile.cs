namespace ERP.Modularity.Features;

public sealed record CapabilityView(string Key, string Label, string Description, bool Enabled);
public sealed record BusinessProfile(
    string IndustryCode, string? IndustryLabel, string? IndustryGroup, IReadOnlyList<string> SuggestedModules, IReadOnlyList<string> MissingModules,
    IReadOnlyList<CapabilityView> Capabilities, IReadOnlyList<string> Units, IReadOnlyList<string> ItemCategories, IReadOnlyList<string> DashboardFocus);
public sealed record ProfileListsInput(IReadOnlyList<string>? Units, IReadOnlyList<string>? ItemCategories);
public sealed record CapabilitiesInput(IReadOnlyList<string> Disabled);
