using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Domain.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modularity.Features;

public sealed record MeUser(Guid Id, string Email, string FullName, bool MustChangePassword);
public sealed record MeWorkspace(Guid Id, string Code, string Name, string IndustryCode, string CountryCode, string BaseCurrency, string TimeZone, string DefaultLocale);
public sealed record MeLegalEntity(Guid Id, string Code, string Name, string BaseCurrency);
public sealed record MeModule(string Key, string UiRoute);

/// <summary>Everything the UI needs after sign-in: identity, workspace, permissions, legal entities and enabled modules.</summary>
public sealed record MeDto(
    MeUser User, MeWorkspace Workspace, IReadOnlyList<WorkspaceOption> Workspaces, IReadOnlyList<string> Permissions,
    IReadOnlyList<MeLegalEntity> LegalEntities, bool AllLegalEntities, IReadOnlyList<MeModule> EnabledModules);

public sealed record GetMe : IQuery<MeDto>;

public sealed record MenuItemDto(string Id, string Label, string Route, string? Icon, string? Permission, string Module);
public sealed record MenuGroupDto(string Id, string? Label, int Order, IReadOnlyList<MenuItemDto> Items);

/// <summary>Server-driven navigation: core items plus the groups of enabled modules, filtered by the caller's permissions.</summary>
public sealed record GetNavigationMenu : IQuery<IReadOnlyList<MenuGroupDto>>;

internal sealed class SessionHandlers(
    ICurrentUser user, IUserDirectory directory, IAuthService auth, IReadRepository<Organization> organizations,
    IReadRepository<LegalEntity> legalEntities, ModuleRegistry modules, IModuleAccess access) :
    IRequestHandler<GetMe, MeDto>,
    IRequestHandler<GetNavigationMenu, IReadOnlyList<MenuGroupDto>>
{
    public async Task<MeDto> Handle(GetMe q, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new ForbiddenException();
        var profile = await directory.GetProfileAsync(userId, ct) ?? throw new NotFoundException();
        var org = await organizations.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);
        var (permissions, allowedEntities, enabled) = await auth.ResolveAccessAsync(userId, org.Id, ct);
        var entities = await legalEntities.Query().Where(e => allowedEntities == null || allowedEntities.Contains(e.Id)).OrderBy(e => e.Code)
            .Select(e => new MeLegalEntity(e.Id, e.Code, e.Name, e.BaseCurrency)).ToListAsync(ct);
        return new(
            new(profile.Id, profile.Email, profile.FullName, profile.MustChangePassword),
            new(org.Id, org.Code, org.Name, org.IndustryCode, org.CountryCode, org.BaseCurrency, org.TimeZone, org.DefaultLocale),
            await auth.WorkspacesForAsync(userId, ct),
            permissions.OrderBy(p => p).ToList(),
            entities,
            allowedEntities is null,
            modules.Modules.Where(m => m.Manifest.IsCore || enabled.Contains(m.Manifest.Key)).Select(m => new MeModule(m.Manifest.Key, m.Manifest.UiRoute)).ToList());
    }

    public async Task<IReadOnlyList<MenuGroupDto>> Handle(GetNavigationMenu q, CancellationToken ct)
    {
        var enabled = await access.EnabledModulesAsync(ct);
        return CoreMenu.Items.Select(i => (Item: i, Module: "core"))
            .Concat(modules.Modules.Where(m => m.Manifest.IsCore || enabled.Contains(m.Manifest.Key)).SelectMany(m => m.Menu.Select(i => (Item: i, Module: m.Manifest.Key))))
            .Where(x => x.Item.Permission is null || user.HasPermission(x.Item.Permission))
            .GroupBy(x => x.Item.GroupId)
            .Select(g => new MenuGroupDto(g.Key, g.First().Item.GroupLabel, g.First().Item.GroupOrder,
                g.OrderBy(x => x.Item.Order).Select(x => new MenuItemDto(x.Item.Id, x.Item.Label, x.Item.Route, x.Item.Icon, x.Item.Permission, x.Module)).ToList()))
            .OrderBy(g => g.Order).ToList();
    }
}
