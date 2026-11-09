using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Templates;
using ERP.Domain.Organizations;

namespace ERP.Modularity.Features;

internal sealed class GetBusinessProfileHandler(
    IReadRepository<Organization> organizations, ModuleRegistry modules, IModuleAccess access, ICapabilityAccess capabilities, ISettingsService settings, ICurrentUser user)
    : IQueryHandler<GetBusinessProfileQuery, BusinessProfile>
{
    public async Task<BusinessProfile> Handle(GetBusinessProfileQuery query, CancellationToken ct)
    {
        var org = await organizations.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);
        var template = SetupTemplates.All(modules).LastOrDefault(t => t.Key == org.IndustryCode);
        var enabled = await access.EnabledModulesAsync(ct);
        var suggested = template?.SuggestedModules.Where(modules.IsInstalled).ToList() ?? [];
        var disabled = await capabilities.DisabledAsync(ct);
        return new(org.IndustryCode, template?.Label, template?.Group, suggested, suggested.Where(m => !enabled.Contains(m)).ToList(),
            CoreCapabilities.All.Select(c => new CapabilityView(c.Key, c.Label, c.Description, !disabled.Contains(c.Key))).ToList(),
            await ListAsync(ModuleProvisioning.UnitsKey, ct), await ListAsync(ModuleProvisioning.ItemCategoriesKey, ct), await ListAsync(ModuleProvisioning.DashboardFocusKey, ct));
    }

    private async Task<IReadOnlyList<string>> ListAsync(string key, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct: ct);
        return string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<string[]>(raw) ?? [];
    }
}
