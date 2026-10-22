using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Application.Templates;
using ERP.Domain.Finance;
using ERP.Domain.Identity;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Modularity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed record OnboardingRequest(
    string WorkspaceName, string SignInKey, string Industry, string CountryCode, string BaseCurrency, string? TimeZone,
    string? LegalEntityName, string AdminEmail, string AdminName, string AdminPassword);

public sealed record OnboardingResult(Guid OrgId, Guid LegalEntityId, Guid AdminUserId, IReadOnlyList<string> EnabledModules, IReadOnlyList<string> UnavailableModules);

public sealed record IndustryView(string Key, string Label, string Description, string Availability, IReadOnlyList<string> SuggestedModules, IReadOnlyList<string> InstalledModules, int SortOrder);

/// <summary>Creates a workspace from an industry template: org, first legal entity, core + industry seed, modules and the admin user.</summary>
public sealed class OnboardingService(AppDbContext db, UserManager<AppUser> users, IOrgContext org, ModuleRegistry modules, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
{
    public IReadOnlyList<IndustryTemplate> Industries()
        => CoreTemplates.Industries.Concat(modules.Modules.SelectMany(m => m.IndustryTemplates)).GroupBy(i => i.Key).Select(g => g.Last())
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Label).ToList();

    public IReadOnlyList<IndustryView> IndustryViews() => Industries().Select(i => new IndustryView(
        i.Key, i.Label, i.Description, i.Availability.ToString(), i.SuggestedModules,
        i.SuggestedModules.Where(modules.IsInstalled).ToList(), i.SortOrder)).ToList();

    public async Task<OnboardingResult> CreateWorkspaceAsync(OnboardingRequest r, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var industry = Industries().FirstOrDefault(i => i.Key == r.Industry);
        if (industry is null) errors["industry"] = [$"Unknown industry. Allowed: {string.Join(", ", Industries().Select(i => i.Key))}."];
        var key = (r.SignInKey ?? "").Trim().ToLowerInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9][a-z0-9-]{1,48}$")) errors["signInKey"] = ["Use 2-49 lower-case letters, digits or dashes."];
        if (string.IsNullOrWhiteSpace(r.WorkspaceName)) errors["workspaceName"] = ["Workspace name is required."];
        if ((r.CountryCode ?? "").Length != 2) errors["countryCode"] = ["Use a 2-letter ISO country code."];
        if ((r.BaseCurrency ?? "").Length != 3) errors["baseCurrency"] = ["Use a 3-letter ISO currency code."];
        if (string.IsNullOrWhiteSpace(r.AdminEmail)) errors["adminEmail"] = ["Admin email is required."];
        if (errors.Count > 0) throw new FieldValidationException(errors);
        if (await db.Organizations.AnyAsync(o => o.Code == key, ct)) throw new ConflictException("That sign-in key is already taken.");

        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

        var organization = new Organization
        {
            Name = r.WorkspaceName.Trim(), Code = key, IndustryCode = industry!.Key, CountryCode = r.CountryCode.ToUpperInvariant(),
            BaseCurrency = r.BaseCurrency.ToUpperInvariant(), TimeZone = r.TimeZone ?? "UTC",
        };
        db.Organizations.Add(organization);

        // The admin login: reuse an existing account (multi-workspace) if the password matches.
        var user = await users.FindByEmailAsync(r.AdminEmail.Trim());
        if (user is null)
        {
            user = new AppUser { Id = ERP.Domain.Common.SequentialGuid.NewGuid(), UserName = r.AdminEmail.Trim(), Email = r.AdminEmail.Trim(), FullName = r.AdminName.Trim(), EmailConfirmed = true, CreatedAt = DateTime.UtcNow };
            var created = await users.CreateAsync(user, r.AdminPassword);
            if (!created.Succeeded) throw new FieldValidationException(new Dictionary<string, string[]> { ["adminPassword"] = created.Errors.Select(e => e.Description).ToArray() });
        }
        else if (!await users.CheckPasswordAsync(user, r.AdminPassword))
            throw new FieldValidationException("adminEmail", "This email already has an account. Enter that account's password to add a new workspace to it.");

        using (org.Use(organization.Id))
        {
            await db.SaveChangesAsync(ct); // org row first (audited under its own id)

            var entity = new LegalEntity
            {
                Code = "ENT-0001", Name = (r.LegalEntityName ?? r.WorkspaceName).Trim(), CountryCode = organization.CountryCode,
                BaseCurrency = organization.BaseCurrency,
            };
            db.LegalEntities.Add(entity);

            foreach (var c in TaxTemplates.Currencies)
                if (!await db.Currencies.AnyAsync(x => x.Code == c.Code, ct) && !db.Currencies.Local.Any(x => x.Code == c.Code))
                    db.Currencies.Add(new Currency { Code = c.Code, Name = c.Name, Symbol = c.Symbol, Decimals = c.Decimals });

            foreach (var a in CoreTemplates.ChartOfAccounts.Concat(industry.Accounts ?? []).DistinctBy(a => a.Code))
                db.Accounts.Add(new Account { Code = a.Code, Name = a.Name, Type = a.Type, SystemRole = a.SystemRole, IsBank = a.IsBank, IsSystem = a.SystemRole is not null, ReportingGroup = a.ReportingGroup });

            foreach (var t in TaxTemplates.ForCountry(organization.CountryCode))
                db.TaxCodes.Add(new TaxCode { Code = t.Code, Name = t.Name, Kind = t.Kind, Rate = t.Rate, IsDefault = t.IsDefault, IsRecoverable = t.IsRecoverable });

            foreach (var s in CoreTemplates.Sequences.Concat(industry.Sequences ?? []))
                db.NumberSequences.Add(new NumberSequence { DocumentType = s.DocumentType, Prefix = s.Prefix, Pattern = s.Pattern, ResetYearly = s.ResetYearly, Year = 0, NextValue = 1 });
            // The first legal entity code was allocated above.
            db.NumberSequences.Local.First(s => s.DocumentType == "legalentity").NextValue = 2;

            Role? adminRole = null;
            foreach (var rt in CoreTemplates.Roles.Concat(industry.Roles ?? []))
            {
                var role = new Role { Name = rt.Name, Description = rt.Description, IsBuiltIn = true, ModuleKey = rt.ModuleKey };
                role.Permissions.AddRange(rt.Permissions.Distinct().Select(p => new RolePermission { Permission = p }));
                db.Roles.Add(role);
                if (rt.Permissions.Contains(CorePermissions.All)) adminRole = role;
            }

            foreach (var (k, v) in industry.Settings ?? new Dictionary<string, string>())
                db.Settings.Add(new Setting { Key = k, Value = v });

            var enabled = industry.SuggestedModules.Where(modules.IsInstalled).Distinct().ToList();
            foreach (var m in enabled)
                db.OrganizationModules.Add(new OrganizationModule { ModuleKey = m, IsEnabled = true, EnabledAt = DateTime.UtcNow });

            db.OrganizationMembers.Add(new OrganizationMember { UserId = user.Id, JoinedAt = DateTime.UtcNow });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole!.Id });
            db.ApprovalPolicies.Add(new ApprovalPolicy { DocumentType = "general", IsActive = true });

            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            cache.Remove(ModuleAccess.CacheKey(organization.Id));

            return new(organization.Id, entity.Id, user.Id, enabled, industry.SuggestedModules.Where(m => !modules.IsInstalled(m)).ToList());
        }
    }
}
