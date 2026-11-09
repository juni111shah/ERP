using ERP.Application.Common;

namespace ERP.Modularity.Features;

internal sealed class ListPlatformModulesHandler(ModuleRegistry modules) : IQueryHandler<ListPlatformModulesQuery, IReadOnlyList<PlatformModuleView>>
{
    public Task<IReadOnlyList<PlatformModuleView>> Handle(ListPlatformModulesQuery query, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlatformModuleView>>(modules.Modules.Where(m => !m.Manifest.Hidden).Select(m => new PlatformModuleView(m.Manifest.Key, m.Manifest.Name, m.Manifest.Description, m.Manifest.Category, m.Manifest.IsCore, m.Manifest.Requires ?? [])).OrderBy(m => m.Name).ToList());
}
