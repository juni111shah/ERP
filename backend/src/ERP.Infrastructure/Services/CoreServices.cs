using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Common;
using ERP.Domain.Organizations;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Modularity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ERP.Infrastructure.Services;

public sealed class PermissionCatalog : IPermissionCatalog
{
    public PermissionCatalog(ModuleRegistry modules)
    {
        All = CorePermissions.Definitions.Concat(modules.Modules.SelectMany(m => m.Permissions)).DistinctBy(p => p.Key).ToList();
        _keys = All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    }

    private readonly HashSet<string> _keys;
    public IReadOnlyList<PermissionDefinition> All { get; }

    public IReadOnlySet<string> Expand(IEnumerable<string> granted)
    {
        var list = granted.ToList();
        if (list.Contains(CorePermissions.All)) return _keys.ToHashSet(StringComparer.Ordinal);
        return list.Where(_keys.Contains).ToHashSet(StringComparer.Ordinal);
    }
}

public sealed class CustomizableEntityRegistry : ICustomizableEntityRegistry
{
    public static IReadOnlyList<CustomizableEntityDefinition> Core { get; } =
    [
        new("party", "Party (customer, vendor)", typeof(Party), "core", CorePermissions.PartiesRead),
        new("item", "Item (product, service)", typeof(Item), "core", CorePermissions.ItemsRead),
        new("legalentity", "Legal entity", typeof(LegalEntity), "core", CorePermissions.EntitiesRead),
        new("businessunit", "Business unit", typeof(BusinessUnit), "core", CorePermissions.BusinessUnitsRead),
        new("account", "Account", typeof(ERP.Domain.Finance.Account), "core", CorePermissions.AccountsRead),
    ];

    private readonly Dictionary<string, CustomizableEntityDefinition> _all;

    public CustomizableEntityRegistry(ModuleRegistry modules)
    {
        _all = new(StringComparer.Ordinal);
        foreach (var d in Core.Concat(modules.Modules.SelectMany(m => m.CustomizableEntities)))
        {
            if (!typeof(IHasCustomFields).IsAssignableFrom(d.ClrType) || !typeof(Entity).IsAssignableFrom(d.ClrType))
                throw new InvalidOperationException($"{d.ClrType.Name} must derive from Entity and implement IHasCustomFields.");
            _all.Add(d.Key, d);
        }
    }

    public IReadOnlyCollection<CustomizableEntityDefinition> All => _all.Values;
    public CustomizableEntityDefinition? Find(string key) => _all.GetValueOrDefault(key);
}

public sealed class ModuleAccess(IReadRepository<OrganizationModule> organizationModules, IOrgContext org, ModuleRegistry registry, IMemoryCache cache) : IModuleAccess
{
    public static string CacheKey(Guid orgId) => $"org:{orgId}:modules";

    public void Invalidate()
    {
        if (org.OrgId is { } orgId) cache.Remove(CacheKey(orgId));
    }

    public async Task<bool> IsEnabledAsync(string moduleKey, CancellationToken ct = default)
    {
        var m = registry.Find(moduleKey);
        if (m is null) return false;
        if (m.Manifest.IsCore) return true;
        return (await EnabledModulesAsync(ct)).Contains(moduleKey);
    }

    public async Task<IReadOnlySet<string>> EnabledModulesAsync(CancellationToken ct = default)
    {
        if (org.OrgId is not { } orgId) return new HashSet<string>();
        return await cache.GetOrCreateAsync(CacheKey(orgId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var keys = await organizationModules.Query(new Specification<OrganizationModule>(x => x.IsEnabled)).Select(x => x.ModuleKey).ToListAsync(ct);
            return (IReadOnlySet<string>)keys.Where(registry.IsInstalled).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }) ?? new HashSet<string>();
    }
}

public sealed class LocalFileStore(IConfiguration config) : IFileStore
{
    private string Root => Path.GetFullPath(config["Storage:Root"] ?? Path.Combine(AppContext.BaseDirectory, "storage"));

    public async Task<string> SaveAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
        return key;
    }

    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct = default) => Task.FromResult<Stream>(File.OpenRead(PathFor(storageKey)));

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var p = PathFor(storageKey);
        if (File.Exists(p)) File.Delete(p);
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        var rootWithSep = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(Root, key));
        if (!full.StartsWith(rootWithSep, StringComparison.Ordinal)) throw new InvalidOperationException("Invalid storage key.");
        return full;
    }
}

/// <summary>Used when no real-time transport is registered (tests, background jobs); the API host swaps in SignalR.</summary>
public sealed class NullNotificationPusher : INotificationPusher { public Task PushAsync(Guid userId, object payload, CancellationToken ct = default) => Task.CompletedTask; }

/// <summary>Every setting known to this deployment: the core's plus those declared by installed modules.</summary>
public sealed class SettingsCatalog(ModuleRegistry modules) : ISettingsCatalog
{
    public IReadOnlyList<(SettingDefinition Def, string Module)> Definitions()
        => ERP.Application.Settings.SettingsService.CoreSettings.Select(d => (d, "core"))
            .Concat(modules.Modules.SelectMany(m => m.Settings.Select(d => (d, m.Manifest.Key)))).ToList();
}
