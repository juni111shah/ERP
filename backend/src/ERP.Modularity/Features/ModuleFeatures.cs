using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modularity.Features;

public sealed record ModuleView(
    string Key, string Name, string Description, string Category, string Version, bool IsCore, IReadOnlyList<string> Requires, string UiRoute, string State);

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

internal sealed class ModuleHandlers(ModuleRegistry modules, IModuleAccess access, IRepository<OrganizationModule> organizationModules) :
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
        return installed.Concat(planned).ToList();
    }

    public async Task<ModuleState> Handle(SetModuleState cmd, CancellationToken ct)
    {
        var module = modules.Find(cmd.Key) ?? throw new NotFoundException("Module not installed.");
        if (module.Manifest.IsCore) throw new ConflictException("Core modules cannot be disabled.");
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
