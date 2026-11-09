using ERP.Application.Templates;

namespace ERP.Modularity.Features;

/// <summary>Every business type a workspace can be: the core ones and those of the installed modules (a later one with the same key wins).</summary>
internal static class SetupTemplates
{
    public static IReadOnlyList<IndustryTemplate> All(ModuleRegistry modules) => [.. CoreTemplates.Industries, .. modules.Modules.SelectMany(m => m.IndustryTemplates)];
}
