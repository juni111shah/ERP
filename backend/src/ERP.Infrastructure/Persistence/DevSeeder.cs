using ERP.Domain.Identity;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Development seed: two workspaces built through the normal onboarding path. Credentials come from configuration
/// (appsettings.Development.json, clearly marked dev-only). Never enable in production.
/// </summary>
public static class DevSeeder
{
    public static async Task SeedAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var section = config.GetSection("Seed");
        if (!section.GetValue<bool>("Enabled")) return;
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DevSeeder");
        if (await db.Organizations.AnyAsync(ct)) return;

        var password = section["AdminPassword"] ?? throw new InvalidOperationException("Seed:AdminPassword is required when seeding.");
        var adminEmail = section["AdminEmail"] ?? "admin@demo.local";
        var onboarding = sp.GetRequiredService<OnboardingService>();

        var demo = await onboarding.CreateWorkspaceAsync(new("Demo Workspace", "demo", "generic", "QA", "QAR", "Asia/Qatar", "Al Rayyan Trading W.L.L.", adminEmail, "Demo Administrator", password), ct);
        var re = await onboarding.CreateWorkspaceAsync(new("Demo Properties", "demo-re", "realestate", "QA", "QAR", "Asia/Qatar", "Lusail Properties W.L.L.", section["MultiWorkspaceEmail"] ?? "owner@demo.local", "Workspace Owner", password), ct);
        // Qatar company details on the two demo legal entities (display name, registered name, CR number, address, contact).
        var details = new[]
        {
            (demo.LegalEntityId, "Al Rayyan Trading W.L.L.", "CR-84215", "Corniche Street, Doha Tower, 18th Floor, West Bay, Doha, Qatar", "info@alrayyan-trading.example.com", "+974 4411 2200"),
            (re.LegalEntityId, "Lusail Properties W.L.L.", "CR-73908", "Marina Promenade, Lusail City, Doha, Qatar", "info@lusail-properties.example.com", "+974 4412 3300"),
        };
        foreach (var (id, legalName, cr, address, email, phone) in details)
        {
            var entity = await db.LegalEntities.IgnoreQueryFilters().FirstAsync(e => e.Id == id, ct);
            entity.LegalName = legalName; entity.RegistrationNo = cr; entity.Address = address; entity.Email = email; entity.Phone = phone;
        }
        await db.SaveChangesAsync(ct);
        // The owner also joins the generic workspace, to exercise the workspace picker.
        var users = sp.GetRequiredService<UserManager<AppUser>>();
        var orgCtx = sp.GetRequiredService<ERP.Application.Abstractions.IOrgContext>();
        using (orgCtx.Use(demo.OrgId))
        {
            var viewer = await db.Roles.FirstAsync(r => r.Name == "Viewer", ct);
            db.OrganizationMembers.Add(new OrganizationMember { UserId = re.AdminUserId, JoinedAt = DateTime.UtcNow });
            db.UserRoles.Add(new UserRole { UserId = re.AdminUserId, RoleId = viewer.Id });
            await db.SaveChangesAsync(ct);
        }
        log.LogWarning("Seeded DEV workspaces 'demo' ({Admin}) and 'demo-re' (multi-workspace owner). Dev-only credentials from configuration.", adminEmail);
    }
}
