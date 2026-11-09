using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Domain.Organizations;

namespace ERP.Modularity.Features;

internal sealed class GetNavigationMenuHandler(
    ICurrentUser user, IReadRepository<Organization> organizations, ModuleRegistry modules, IModuleAccess access, ICapabilityAccess capabilities)
    : IQueryHandler<GetNavigationMenuQuery, IReadOnlyList<MenuGroupDto>>
{
    public async Task<IReadOnlyList<MenuGroupDto>> Handle(GetNavigationMenuQuery query, CancellationToken ct)
    {
        // 1. Enabled modules, hidden core areas and the industry's own words for menu items.
        var enabled = await access.EnabledModulesAsync(ct);
        var hidden = await capabilities.HiddenMenuItemsAsync(ct);
        var org = await organizations.FindAsync(user.OrgId ?? Guid.Empty, ct);
        var words = modules.Modules.SelectMany(m => m.IndustryTemplates).FirstOrDefault(t => t.Key == org?.IndustryCode)?.Terminology;

        // 2. Core items and module items the caller may open, grouped and ordered.
        return CoreMenu.Items.Select(i => (Item: i, Module: "core"))
            .Concat(modules.Modules.Where(m => m.Manifest.IsCore || enabled.Contains(m.Manifest.Key)).SelectMany(m => m.Menu.Select(i => (Item: i, Module: m.Manifest.Key))))
            .Where(x => !hidden.Contains(x.Item.Id) && (x.Item.Permission is null || user.HasPermission(x.Item.Permission)))
            .GroupBy(x => x.Item.GroupId)
            .Select(g => new MenuGroupDto(g.Key, g.First().Item.GroupLabel, g.First().Item.GroupOrder,
                g.OrderBy(x => x.Item.Order).Select(x => new MenuItemDto(x.Item.Id, words?.GetValueOrDefault(x.Item.Id) ?? x.Item.Label, x.Item.Route, x.Item.Icon, x.Item.Permission, x.Module)).ToList()))
            .OrderBy(g => g.Order).ToList();
    }
}
