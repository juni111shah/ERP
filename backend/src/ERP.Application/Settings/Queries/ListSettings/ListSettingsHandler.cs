using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Settings;

internal sealed class ListSettingsHandler(ISettingsService settings, ISettingsCatalog catalog, IModuleAccess modules) : IQueryHandler<ListSettingsQuery, IReadOnlyList<SettingView>>
{
    public async Task<IReadOnlyList<SettingView>> Handle(ListSettingsQuery query, CancellationToken ct)
    {
        var enabled = await modules.EnabledModulesAsync(ct);
        var list = new List<SettingView>();
        foreach (var (def, module) in catalog.Definitions().Where(d => d.Module == "core" || enabled.Contains(d.Module)))
        {
            var raw = await settings.GetRawAsync(def.Key, ct: ct) ?? "null";
            list.Add(new(def.Key, def.Label, module, def.Type, def.Description, JsonSerializer.Deserialize<JsonElement>(raw)));
        }
        return list;
    }
}
