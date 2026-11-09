using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modularity.Features;

internal sealed class ListModulesHandler(ModuleRegistry modules, IModuleAccess access) : IQueryHandler<ListModulesQuery, IReadOnlyList<ModuleView>>
{
    public async Task<IReadOnlyList<ModuleView>> Handle(ListModulesQuery query, CancellationToken ct)
    {
        var enabled = await access.EnabledModulesAsync(ct);
        var installed = modules.Modules.Where(m => !m.Manifest.Hidden).Select(m => new ModuleView(m.Manifest.Key, m.Manifest.Name, m.Manifest.Description, m.Manifest.Category,
            m.Manifest.Version, m.Manifest.IsCore, m.Manifest.Requires ?? [], m.Manifest.UiRoute,
            m.Manifest.IsCore || enabled.Contains(m.Manifest.Key) ? "enabled" : "disabled"));
        var planned = ModuleRegistry.Planned.Where(p => !modules.IsInstalled(p.Key)).Select(p => new ModuleView(p.Key, p.Name, p.Description, p.Category,
            p.Version, p.IsCore, p.Requires ?? [], p.UiRoute, "unavailable"));
        return installed.Concat(planned).ToList();
    }
}
