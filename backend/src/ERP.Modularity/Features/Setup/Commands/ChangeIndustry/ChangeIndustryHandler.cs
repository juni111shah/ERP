using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using MediatR;

namespace ERP.Modularity.Features;

internal sealed class ChangeIndustryHandler(
    IRepository<Organization> organizations, ModuleRegistry modules, IModuleAccess access, ISettingsService settings,
    IRoleRepository roles, IAccountRepository accounts, IUnitOfWork unitOfWork, ICurrentUser user, ISender sender,
    IAccessChangeNotifier accessChanges) : ICommandHandler<ChangeIndustryCommand, ChangeIndustryResult>
{
    public async Task<ChangeIndustryResult> Handle(ChangeIndustryCommand command, CancellationToken ct)
    {
        var template = SetupTemplates.All(modules).LastOrDefault(t => t.Key == command.Input.Industry && !t.Hidden) ?? throw new FieldValidationException("industry", "Choose a business type from the list.");
        var org = await organizations.GetForUpdateAsync(user.OrgId ?? throw new ForbiddenException(), ct);

        var rolesBefore = await roles.ListNamesAsync(ct);
        var codesBefore = await accounts.ListCodesAsync(ct);

        // Enable first: a module the plan does not allow is reported, not forced.
        var wanted = ModuleDependencies.Resolve(template.SuggestedModules, modules.Find, null);
        var enabledNow = await access.EnabledModulesAsync(ct);
        var added = new List<string>();
        var refused = new List<string>();
        foreach (var key in wanted.Where(m => !enabledNow.Contains(m)))
        {
            try { await sender.Send(new SetModuleStateCommand(key, true), ct); added.Add(key); }
            catch (Exception ex) when (ex is ConflictException or ForbiddenException) { refused.Add(key); }
        }

        var existingRoles = await roles.ListNamesAsync(ct);
        var moduleTemplates = wanted.SelectMany(m => modules.Find(m)!.IndustryTemplates).ToList();
        var newRoles = (template.Roles ?? []).Concat(moduleTemplates.SelectMany(t => t.Roles ?? [])).DistinctBy(r => r.Name).Where(r => !existingRoles.Contains(r.Name)).ToList();
        foreach (var rt in newRoles)
        {
            var role = new Role { Name = rt.Name, Description = rt.Description, IsBuiltIn = true, ModuleKey = rt.ModuleKey };
            role.Permissions.AddRange(rt.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
            roles.Add(role);
        }

        var existingCodes = await accounts.ListCodesAsync(ct);
        var newAccounts = (template.Accounts ?? []).Concat(moduleTemplates.SelectMany(t => t.Accounts ?? [])).DistinctBy(a => a.Code).Where(a => !existingCodes.Contains(a.Code)).ToList();
        foreach (var a in newAccounts)
            accounts.Add(new Account { Code = a.Code, Name = a.Name, Type = a.Type, SystemRole = a.SystemRole, IsBank = a.IsBank, IsSystem = a.SystemRole is not null, ReportingGroup = a.ReportingGroup });

        foreach (var (key, value) in ModuleProvisioning.ProfileSettings(template).Where(s => s.Key != CoreCapabilities.SettingKey))
            await settings.SetRawAsync(key, value, ct: ct);

        org.IndustryCode = template.Key;
        await unitOfWork.SaveChangesAsync(ct);
        // The industry's own words for menu items (and any modules switched on above) reach every member at once.
        await accessChanges.WorkspaceChangedAsync(org.Id, AccessChangeReasons.IndustryChanged, ct);
        var rolesNow = await roles.ListNamesAsync(ct);
        var codesNow = await accounts.ListCodesAsync(ct);
        return new(template.Key, added, rolesNow.Except(rolesBefore).ToList(), codesNow.Except(codesBefore).ToList(), refused);
    }
}
