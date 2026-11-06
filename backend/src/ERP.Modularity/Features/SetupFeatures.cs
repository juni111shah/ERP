using System.Text.Json;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modularity.Features;

public sealed record CapabilityView(string Key, string Label, string Description, bool Enabled);
public sealed record BusinessProfile(
    string IndustryCode, string? IndustryLabel, string? IndustryGroup, IReadOnlyList<string> SuggestedModules, IReadOnlyList<string> MissingModules,
    IReadOnlyList<CapabilityView> Capabilities, IReadOnlyList<string> Units, IReadOnlyList<string> ItemCategories, IReadOnlyList<string> DashboardFocus);
public sealed record ProfileListsInput(IReadOnlyList<string>? Units, IReadOnlyList<string>? ItemCategories);
public sealed record CapabilitiesInput(IReadOnlyList<string> Disabled);
public sealed record ChangeIndustryInput(string Industry);
public sealed record ChangeIndustryResult(string IndustryCode, IReadOnlyList<string> ModulesEnabled, IReadOnlyList<string> RolesAdded, IReadOnlyList<string> AccountsAdded, IReadOnlyList<string> ModulesNotAllowed);
public sealed record NumberSequenceView(string DocumentType, string Prefix, string Pattern, bool ResetYearly, long NextValue, string Example);
public sealed record NumberSequenceInput(string Prefix, string Pattern, bool ResetYearly);

public sealed record GetBusinessProfile : IQuery<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
public sealed record SetCapabilities(CapabilitiesInput Input) : ICommand<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
public sealed record SetProfileLists(ProfileListsInput Input) : ICommand<BusinessProfile>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
/// <summary>
/// Moves a workspace to another business type. Only ever adds: modules, roles and accounts it does not have yet. Nothing is switched off,
/// removed or renumbered, because the workspace may already hold data in them.
/// </summary>
public sealed record ChangeIndustry(ChangeIndustryInput Input) : ICommand<ChangeIndustryResult>, IRequirePermission { public string RequiredPermission => CorePermissions.ModulesManage; }
public sealed record ListNumberSequences : IQuery<IReadOnlyList<NumberSequenceView>>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
public sealed record UpdateNumberSequence(string DocumentType, NumberSequenceInput Input) : ICommand<NumberSequenceView>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }

public sealed class SetProfileListsValidator : AbstractValidator<SetProfileLists>
{
    public SetProfileListsValidator()
    {
        RuleForEach(x => x.Input.Units).NotEmpty().WithMessage("A unit cannot be empty.").MaximumLength(30);
        RuleForEach(x => x.Input.ItemCategories).NotEmpty().WithMessage("A category cannot be empty.").MaximumLength(100);
    }
}

public sealed class UpdateNumberSequenceValidator : AbstractValidator<UpdateNumberSequence>
{
    public UpdateNumberSequenceValidator()
    {
        RuleFor(x => x.Input.Prefix).NotEmpty().WithMessage("Enter a prefix.").MaximumLength(20).Matches("^[A-Za-z0-9/_-]+$").WithMessage("Use letters, digits, dash, underscore or slash.");
        RuleFor(x => x.Input.Pattern).NotEmpty().WithMessage("Enter a pattern.").MaximumLength(60)
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(p ?? "", @"\{seq:[1-9]\d?\}")).WithMessage("The pattern must contain {seq:N}, such as {seq:6}.")
            .Must(p => System.Text.RegularExpressions.Regex.Replace(p ?? "", @"\{prefix\}|\{yyyy\}|\{yy\}|\{seq:\d+\}", "").IndexOfAny(['{', '}']) < 0).WithMessage("Use only {prefix}, {yyyy}, {yy} and {seq:N}.");
    }
}

internal sealed class SetupHandlers(
    IRepository<Organization> organizations, ModuleRegistry modules, IModuleAccess access, ICapabilityAccess capabilities, ISettingsService settings,
    IRepository<Role> roles, IRepository<Account> accounts, IRepository<NumberSequence> sequences, IUnitOfWork unitOfWork, ICurrentUser user, ISender sender, IClock clock) :
    IRequestHandler<GetBusinessProfile, BusinessProfile>,
    IRequestHandler<SetCapabilities, BusinessProfile>,
    IRequestHandler<SetProfileLists, BusinessProfile>,
    IRequestHandler<ChangeIndustry, ChangeIndustryResult>,
    IRequestHandler<ListNumberSequences, IReadOnlyList<NumberSequenceView>>,
    IRequestHandler<UpdateNumberSequence, NumberSequenceView>
{
    private IReadOnlyList<IndustryTemplate> Templates => [.. CoreTemplates.Industries, .. modules.Modules.SelectMany(m => m.IndustryTemplates)];

    public async Task<BusinessProfile> Handle(GetBusinessProfile q, CancellationToken ct)
    {
        var org = await organizations.GetAsync(user.OrgId ?? throw new ForbiddenException(), ct);
        var template = Templates.LastOrDefault(t => t.Key == org.IndustryCode);
        var enabled = await access.EnabledModulesAsync(ct);
        var suggested = template?.SuggestedModules.Where(modules.IsInstalled).ToList() ?? [];
        var disabled = await capabilities.DisabledAsync(ct);
        return new(org.IndustryCode, template?.Label, template?.Group, suggested, suggested.Where(m => !enabled.Contains(m)).ToList(),
            CoreCapabilities.All.Select(c => new CapabilityView(c.Key, c.Label, c.Description, !disabled.Contains(c.Key))).ToList(),
            await ListAsync(ModuleProvisioning.UnitsKey, ct), await ListAsync(ModuleProvisioning.ItemCategoriesKey, ct), await ListAsync(ModuleProvisioning.DashboardFocusKey, ct));
    }

    public async Task<BusinessProfile> Handle(SetCapabilities cmd, CancellationToken ct)
    {
        var unknown = cmd.Input.Disabled.FirstOrDefault(k => CoreCapabilities.Find(k) is null);
        if (unknown is not null) throw new FieldValidationException("disabled", $"Unknown area '{unknown}'.");
        await settings.SetRawAsync(CoreCapabilities.SettingKey, JsonSerializer.Serialize(cmd.Input.Disabled.Distinct()), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await Handle(new GetBusinessProfile(), ct);
    }

    public async Task<BusinessProfile> Handle(SetProfileLists cmd, CancellationToken ct)
    {
        if (cmd.Input.Units is { } units) await settings.SetRawAsync(ModuleProvisioning.UnitsKey, JsonSerializer.Serialize(Clean(units)), ct: ct);
        if (cmd.Input.ItemCategories is { } categories) await settings.SetRawAsync(ModuleProvisioning.ItemCategoriesKey, JsonSerializer.Serialize(Clean(categories)), ct: ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await Handle(new GetBusinessProfile(), ct);
    }

    public async Task<ChangeIndustryResult> Handle(ChangeIndustry cmd, CancellationToken ct)
    {
        var template = Templates.LastOrDefault(t => t.Key == cmd.Input.Industry && !t.Hidden) ?? throw new FieldValidationException("industry", "Choose a business type from the list.");
        var org = await organizations.GetForUpdateAsync(user.OrgId ?? throw new ForbiddenException(), ct);

        // Enable first: a module the plan does not allow is reported, not forced.
        var wanted = ModuleDependencies.Resolve(template.SuggestedModules, modules.Find, null);
        var enabledNow = await access.EnabledModulesAsync(ct);
        var added = new List<string>();
        var refused = new List<string>();
        foreach (var key in wanted.Where(m => !enabledNow.Contains(m)))
        {
            try { await sender.Send(new SetModuleState(key, true), ct); added.Add(key); }
            catch (Exception ex) when (ex is ConflictException or ForbiddenException) { refused.Add(key); }
        }

        var existingRoles = await roles.Query().Select(r => r.Name).ToListAsync(ct);
        var moduleTemplates = wanted.SelectMany(m => modules.Find(m)!.IndustryTemplates).Where(t => t.Hidden);
        var newRoles = (template.Roles ?? []).Concat(moduleTemplates.SelectMany(t => t.Roles ?? [])).DistinctBy(r => r.Name).Where(r => !existingRoles.Contains(r.Name)).ToList();
        foreach (var rt in newRoles)
        {
            var role = new Role { Name = rt.Name, Description = rt.Description, IsBuiltIn = true, ModuleKey = rt.ModuleKey };
            role.Permissions.AddRange(rt.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
            roles.Add(role);
        }

        var existingCodes = await accounts.Query().Select(a => a.Code).ToListAsync(ct);
        var newAccounts = (template.Accounts ?? []).Where(a => !existingCodes.Contains(a.Code)).ToList();
        foreach (var a in newAccounts)
            accounts.Add(new Account { Code = a.Code, Name = a.Name, Type = a.Type, SystemRole = a.SystemRole, IsBank = a.IsBank, IsSystem = a.SystemRole is not null, ReportingGroup = a.ReportingGroup });

        foreach (var (key, value) in ModuleProvisioning.ProfileSettings(template).Where(s => s.Key != CoreCapabilities.SettingKey))
            await settings.SetRawAsync(key, value, ct: ct);

        org.IndustryCode = template.Key;
        await unitOfWork.SaveChangesAsync(ct);
        return new(template.Key, added, newRoles.Select(r => r.Name).ToList(), newAccounts.Select(a => a.Code).ToList(), refused);
    }

    public async Task<IReadOnlyList<NumberSequenceView>> Handle(ListNumberSequences q, CancellationToken ct)
    {
        var rows = await sequences.Query(new Specification<NumberSequence>(s => s.LegalEntityId == null)).ToListAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        return rows.GroupBy(s => s.DocumentType).Select(g => g.OrderByDescending(s => s.Year).First()).OrderBy(s => s.DocumentType)
            .Select(s => ToView(s, today)).ToList();
    }

    public async Task<NumberSequenceView> Handle(UpdateNumberSequence cmd, CancellationToken ct)
    {
        var rows = await sequences.Tracked(new Specification<NumberSequence>(s => s.DocumentType == cmd.DocumentType)).ToListAsync(ct);
        if (rows.Count == 0) throw new NotFoundException("That document type has no numbering yet.");
        foreach (var row in rows)
        {
            row.Prefix = cmd.Input.Prefix.Trim();
            row.Pattern = cmd.Input.Pattern.Trim();
            row.ResetYearly = cmd.Input.ResetYearly;
        }
        await unitOfWork.SaveChangesAsync(ct);
        return ToView(rows.OrderByDescending(s => s.Year).First(), DateOnly.FromDateTime(clock.UtcNow));
    }

    private static NumberSequenceView ToView(NumberSequence s, DateOnly today) => new(s.DocumentType, s.Prefix, s.Pattern, s.ResetYearly, s.NextValue, NumberFormatter.Format(s.Pattern, s.Prefix, today, s.NextValue));

    private async Task<IReadOnlyList<string>> ListAsync(string key, CancellationToken ct)
    {
        var raw = await settings.GetRawAsync(key, ct: ct);
        return string.IsNullOrWhiteSpace(raw) ? [] : JsonSerializer.Deserialize<string[]>(raw) ?? [];
    }

    private static IEnumerable<string> Clean(IEnumerable<string> values) => values.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
}
