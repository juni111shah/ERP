using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modularity.Features;

/// <summary><c>Notice</c> is a code the page turns into words, for a module that will not appear even when enabled here.</summary>
public sealed record ModuleView(
    string Key, string Name, string Description, string Category, string Version, bool IsCore, IReadOnlyList<string> Requires, string UiRoute, string State, string? Notice = null);

public sealed record ModuleState(string Key, string State);

/// <summary>Installed and planned modules with their state in the current workspace.</summary>
public sealed record ListModules : IQuery<IReadOnlyList<ModuleView>>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.SettingsRead;
}

/// <summary>Enables or disables an installed module for the current workspace. Module data is never touched.</summary>
public sealed record SetModuleState(string Key, bool Enable) : ICommand<ModuleState>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.ModulesManage;
}

/// <summary>The modules a plan can include (platform console).</summary>
public sealed record PlatformModuleView(string Key, string Name, string Description, string Category, bool IsCore, IReadOnlyList<string> Requires);
public sealed record ListPlatformModules : IQuery<IReadOnlyList<PlatformModuleView>>;

/// <summary>A platform admin switching a module for a client. The caller is checked by the platform-admin policy on the endpoint.</summary>
public sealed record SetClientModule(Guid OrgId, string Key, bool Enable) : ICommand<ModuleState>;

internal sealed class ModuleHandlers(ModuleRegistry modules, IModuleAccess access, IRepository<OrganizationModule> organizationModules, ERP.Application.Platform.ISubscriptionLimits limits, IOrgContext org, IReadRepository<PlatformSetting> settings, IReadRepository<AiProvider> providers) :
    IRequestHandler<ListPlatformModules, IReadOnlyList<PlatformModuleView>>,
    IRequestHandler<SetClientModule, ModuleState>,
    IRequestHandler<ListModules, IReadOnlyList<ModuleView>>,
    IRequestHandler<SetModuleState, ModuleState>
{
    public async Task<IReadOnlyList<ModuleView>> Handle(ListModules q, CancellationToken ct)
    {
        var enabled = await access.EnabledModulesAsync(ct);
        var installed = modules.Modules.Select(m => new ModuleView(m.Manifest.Key, m.Manifest.Name, m.Manifest.Description, m.Manifest.Category,
            m.Manifest.Version, m.Manifest.IsCore, m.Manifest.Requires ?? [], m.Manifest.UiRoute,
            m.Manifest.IsCore || enabled.Contains(m.Manifest.Key) ? "enabled" : "disabled"));
        var planned = ModuleRegistry.Planned.Where(p => !modules.IsInstalled(p.Key)).Select(p => new ModuleView(p.Key, p.Name, p.Description, p.Category,
            p.Version, p.IsCore, p.Requires ?? [], p.UiRoute, "unavailable"));
        var notice = await AssistantNoticeAsync(ct);
        return installed.Concat(planned).Select(m => m.Key == "assistant" ? m with { Notice = notice } : m).ToList();
    }

    /// <summary>Why the workspace assistant would stay hidden. It says nothing about which service is used.</summary>
    private async Task<string?> AssistantNoticeAsync(CancellationToken ct)
    {
        if (!await settings.Query().OrderBy(s => s.CreatedAt).Select(s => s.WorkspaceAssistantEnabled).FirstOrDefaultAsync(ct)) return "assistant-off";
        if (await limits.AssistantMonthlyMessagesAsync(ct) == 0) return "assistant-plan";
        return await providers.AnyAsync(p => p.UseForWorkspace, ct) ? null : "assistant-no-service";
    }

    public Task<IReadOnlyList<PlatformModuleView>> Handle(ListPlatformModules q, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PlatformModuleView>>(modules.Modules.Select(m => new PlatformModuleView(m.Manifest.Key, m.Manifest.Name, m.Manifest.Description, m.Manifest.Category, m.Manifest.IsCore, m.Manifest.Requires ?? [])).OrderBy(m => m.Name).ToList());

    public async Task<ModuleState> Handle(SetClientModule cmd, CancellationToken ct)
    {
        using (org.Use(cmd.OrgId)) return await Handle(new SetModuleState(cmd.Key, cmd.Enable), ct);
    }

    public async Task<ModuleState> Handle(SetModuleState cmd, CancellationToken ct)
    {
        var module = modules.Find(cmd.Key) ?? throw new NotFoundException("Module not installed.");
        if (module.Manifest.IsCore) throw new ConflictException("Core modules cannot be disabled.");
        if (cmd.Enable) await limits.EnsureModuleAllowedAsync(cmd.Key, ct);
        var enabled = await access.EnabledModulesAsync(ct);
        if (cmd.Enable && (module.Manifest.Requires ?? []).FirstOrDefault(r => !enabled.Contains(r)) is { } missing)
            throw new ConflictException($"Enable '{missing}' first.");
        if (!cmd.Enable && modules.Modules.Where(x => enabled.Contains(x.Manifest.Key) && (x.Manifest.Requires ?? []).Contains(cmd.Key)).Select(x => x.Manifest.Key).ToList() is { Count: > 0 } dependents)
            throw new ConflictException($"Disable {string.Join(", ", dependents)} first.");

        var row = await organizationModules.Tracked(new Specification<OrganizationModule>(x => x.ModuleKey == cmd.Key)).FirstOrDefaultAsync(ct);
        if (row is null) { row = new OrganizationModule { ModuleKey = cmd.Key }; organizationModules.Add(row); }
        if (row.IsEnabled != cmd.Enable)
        {
            row.IsEnabled = cmd.Enable;
            if (cmd.Enable) row.EnabledAt = DateTime.UtcNow; else row.DisabledAt = DateTime.UtcNow;
            await organizationModules.SaveChangesAsync(ct);
        }
        access.Invalidate();
        return new(cmd.Key, cmd.Enable ? "enabled" : "disabled");
    }
}
