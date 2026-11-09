namespace ERP.Modularity.Features;

public sealed record ChangeIndustryInput(string Industry);
public sealed record ChangeIndustryResult(string IndustryCode, IReadOnlyList<string> ModulesEnabled, IReadOnlyList<string> RolesAdded, IReadOnlyList<string> AccountsAdded, IReadOnlyList<string> ModulesNotAllowed);
