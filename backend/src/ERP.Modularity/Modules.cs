using ERP.Application.Abstractions;
using ERP.Application.Customization;
using ERP.Application.Security;
using ERP.Application.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Modularity;

public enum ModuleStatus { Available, Planned }

public sealed record ModuleManifest(
    string Key, string Name, string Description, string Category, string Version = "0.1.0",
    bool IsCore = false, IReadOnlyList<string>? Requires = null, ModuleStatus Status = ModuleStatus.Available, string? RoutePrefix = null)
{
    /// <summary>UI route prefix, e.g. "real-estate" for key "realestate".</summary>
    public string UiRoute => RoutePrefix ?? Key;
}

public sealed record MenuItemDefinition(
    string Id, string Label, string Route, string? Icon = null, string? Permission = null, int Order = 0,
    string GroupId = "", string? GroupLabel = null, int GroupOrder = 500);

public sealed record SettingDefinition(string Key, string Label, string DefaultJson, string Type = "number", string? Description = null);

/// <summary>
/// Contract every pluggable module implements. A module registers its services, EF Core model, HTTP endpoints
/// (mounted under /api/v1/m/{key}), permissions, menu items, settings, customizable entities and industry templates.
/// The core never references module assemblies; the host composes them.
/// </summary>
public interface IModule
{
    ModuleManifest Manifest { get; }
    IEnumerable<PermissionDefinition> Permissions { get; }
    IEnumerable<MenuItemDefinition> Menu { get; }
    IEnumerable<SettingDefinition> Settings { get; }
    IEnumerable<CustomizableEntityDefinition> CustomizableEntities { get; }
    IEnumerable<IndustryTemplate> IndustryTemplates { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void ConfigureModel(ModelBuilder modelBuilder);
    /// <summary>Map endpoints on a group already prefixed with /api/v1/m/{key}, authenticated and module-gated.</summary>
    void MapEndpoints(RouteGroupBuilder group);
}

public abstract class ModuleBase : IModule
{
    public abstract ModuleManifest Manifest { get; }
    public virtual IEnumerable<PermissionDefinition> Permissions => [];
    public virtual IEnumerable<MenuItemDefinition> Menu => [];
    public virtual IEnumerable<SettingDefinition> Settings => [];
    public virtual IEnumerable<CustomizableEntityDefinition> CustomizableEntities => [];
    public virtual IEnumerable<IndustryTemplate> IndustryTemplates => [];
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration) { }
    public virtual void ConfigureModel(ModelBuilder modelBuilder) { }
    public virtual void MapEndpoints(RouteGroupBuilder group) { }
}

/// <summary>Installed modules of this deployment (singleton). Per-workspace enablement lives in OrganizationModules.</summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, IModule> _modules;

    public ModuleRegistry(IEnumerable<IModule> modules)
    {
        _modules = new(StringComparer.OrdinalIgnoreCase);
        foreach (var m in modules)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(m.Manifest.Key, "^[a-z][a-z0-9]{1,30}$"))
                throw new InvalidOperationException($"Module key '{m.Manifest.Key}' must be lower-case letters/digits.");
            if (!_modules.TryAdd(m.Manifest.Key, m)) throw new InvalidOperationException($"Duplicate module key '{m.Manifest.Key}'.");
            foreach (var p in m.Permissions)
                if (!p.Key.StartsWith(m.Manifest.Key + ".", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Module '{m.Manifest.Key}' permission '{p.Key}' must start with '{m.Manifest.Key}.'.");
        }
        Signature = string.Join(",", _modules.Keys.OrderBy(k => k));
    }

    public IReadOnlyCollection<IModule> Modules => _modules.Values;
    public string Signature { get; }
    public IModule? Find(string key) => _modules.GetValueOrDefault(key);
    public bool IsInstalled(string key) => _modules.ContainsKey(key);

    /// <summary>Modules on the roadmap; listed as "unavailable" in Settings → Modules and onboarding.</summary>
    public static IReadOnlyList<ModuleManifest> Planned { get; } =
    [
        new("sales", "Sales", "Quotes, sales orders, invoices and receipts.", "Horizontal", Status: ModuleStatus.Planned),
        new("purchasing", "Purchasing", "Purchase orders, vendor bills and payments.", "Horizontal", Status: ModuleStatus.Planned),
        new("pos", "Point of sale", "Counter sales, shifts and cash drawers.", "Retail", Status: ModuleStatus.Planned),
        new("retail", "Retail", "Stores, price lists and promotions.", "Industry", Status: ModuleStatus.Planned),
        new("distribution", "Distribution", "Routes, van sales and wholesale pricing.", "Industry", Status: ModuleStatus.Planned),
        new("hr", "HR and payroll", "Employees, attendance, leave and payroll.", "Horizontal", Status: ModuleStatus.Planned),
        new("projects", "Projects", "Projects, tasks, timesheets and project billing.", "Horizontal", Status: ModuleStatus.Planned),
        new("manufacturing", "Manufacturing", "Bills of materials, work orders and costing.", "Industry", Status: ModuleStatus.Planned),
        new("crm", "CRM", "Leads, opportunities and pipeline.", "Horizontal", Status: ModuleStatus.Planned),
        new("services", "Professional services", "Engagements, retainers and time billing.", "Industry", Status: ModuleStatus.Planned),
    ];
}

/// <summary>Problem type returned when a module is disabled for the workspace.</summary>
public static class ModuleProblems
{
    public const string ModuleDisabledType = "https://erp.local/problems/module-disabled";
}

/// <summary>Endpoint metadata marking an endpoint as owned by a module.</summary>
public sealed record ModuleGateMetadata(string ModuleKey);

/// <summary>
/// Runs after authentication and before authorization: authenticated calls to a module endpoint get 403
/// (type module-disabled) unless the module is enabled for the caller's workspace. The module gate wins over
/// role grants, and module data is never touched.
/// </summary>
public sealed class ModuleGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http)
    {
        var gate = http.GetEndpoint()?.Metadata.GetMetadata<ModuleGateMetadata>();
        if (gate is not null && http.User.Identity?.IsAuthenticated == true)
        {
            var access = http.RequestServices.GetRequiredService<IModuleAccess>();
            if (!await access.IsEnabledAsync(gate.ModuleKey, http.RequestAborted))
            {
                await ApiEnvelope.WriteErrorAsync(http, StatusCodes.Status403Forbidden, "Module not enabled",
                    $"The '{gate.ModuleKey}' module is not enabled for this workspace. Its data is kept and returns when it is enabled again.",
                    ModuleProblems.ModuleDisabledType);
                return;
            }
        }
        await next(http);
    }
}

public static class PermissionEndpointExtensions
{
    public const string PolicyPrefix = "perm:";
    /// <summary>Requires a permission key on an endpoint (policy "perm:&lt;key&gt;").</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission) where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(PolicyPrefix + permission);
}

public static class ModuleDiscovery
{
    /// <summary>Finds IModule implementations in loaded ERP.Modules.* assemblies (and ones present in the base directory).</summary>
    public static IReadOnlyList<IModule> DiscoverFromAppDomain()
    {
        foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "ERP.Modules.*.dll"))
        {
            try { System.Reflection.Assembly.LoadFrom(file); } catch { /* ignore */ }
        }
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("ERP.Modules.", StringComparison.Ordinal) == true)
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .Select(t => (IModule)Activator.CreateInstance(t)!)
            .DistinctBy(m => m.Manifest.Key)
            .ToList();
    }
}
