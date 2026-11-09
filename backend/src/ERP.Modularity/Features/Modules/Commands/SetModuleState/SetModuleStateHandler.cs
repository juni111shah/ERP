using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Organizations;

namespace ERP.Modularity.Features;

internal sealed class SetModuleStateHandler(
    ModuleRegistry modules, IModuleAccess access, IOrganizationModuleRepository organizationModules, ERP.Application.Platform.ISubscriptionLimits limits,
    IAccountRepository accounts, IRoleRepository roles) : ICommandHandler<SetModuleStateCommand, ModuleState>
{
    public async Task<ModuleState> Handle(SetModuleStateCommand command, CancellationToken ct)
    {
        // 1. The module must be installed, optional, allowed by the plan and consistent with what it requires / what requires it.
        var module = modules.Find(command.Key) ?? throw new NotFoundException("Module not installed.");
        if (module.Manifest.IsCore) throw new ConflictException("Core modules cannot be disabled.");
        if (command.Enable) await limits.EnsureModuleAllowedAsync(command.Key, ct);
        var enabled = await access.EnabledModulesAsync(ct);
        if (command.Enable && (module.Manifest.Requires ?? []).FirstOrDefault(r => !enabled.Contains(r)) is { } missing)
            throw new ConflictException($"Enable '{missing}' first.");
        if (!command.Enable && modules.Modules.Where(x => enabled.Contains(x.Manifest.Key) && (x.Manifest.Requires ?? []).Contains(command.Key)).Select(x => x.Manifest.Key).ToList() is { Count: > 0 } dependents)
            throw new ConflictException($"Disable {string.Join(", ", dependents)} first.");

        // 2. Switch it, provisioning what an enabled module needs.
        var row = await organizationModules.FindForUpdateAsync(command.Key, ct);
        if (row is null) { row = new OrganizationModule { ModuleKey = command.Key }; organizationModules.Add(row); }
        if (row.IsEnabled != command.Enable)
        {
            row.IsEnabled = command.Enable;
            if (command.Enable) row.EnabledAt = DateTime.UtcNow; else row.DisabledAt = DateTime.UtcNow;
            if (command.Enable) await ProvisionAsync(module, ct);
            await organizationModules.SaveChangesAsync(ct);
        }
        access.Invalidate();
        return new(command.Key, command.Enable ? "enabled" : "disabled");
    }

    /// <summary>A module's postings need its accounts and its people need its roles, however it was switched on. Only adds what is missing.</summary>
    private async Task ProvisionAsync(IModule module, CancellationToken ct)
    {
        var templates = module.IndustryTemplates.ToList();
        var codes = await accounts.ListCodesAsync(ct);
        var systemRoles = await accounts.ListSystemRolesAsync(ct);
        foreach (var a in templates.SelectMany(t => t.Accounts ?? []).DistinctBy(a => a.Code).Where(a => !codes.Contains(a.Code) && (a.SystemRole is null || !systemRoles.Contains(a.SystemRole))))
            accounts.Add(new ERP.Domain.Finance.Account { Code = a.Code, Name = a.Name, Type = a.Type, SystemRole = a.SystemRole, IsBank = a.IsBank, IsSystem = a.SystemRole is not null, ReportingGroup = a.ReportingGroup });
        var names = await roles.ListNamesAsync(ct);
        foreach (var rt in templates.SelectMany(t => t.Roles ?? []).DistinctBy(r => r.Name).Where(r => !names.Contains(r.Name)))
        {
            var role = new ERP.Domain.Identity.Role { Name = rt.Name, Description = rt.Description, IsBuiltIn = true, ModuleKey = rt.ModuleKey };
            role.Permissions.AddRange(rt.Permissions.Distinct().Select(p => new ERP.Domain.Identity.RolePermission { Permission = p }));
            roles.Add(role);
        }
    }
}
