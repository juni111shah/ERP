using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Runs at every start. Creates the settings row and starter plans when there are none, and makes the account named in
/// Platform:FounderEmail the platform owner. It never changes the password of an account that already exists.
/// </summary>
public sealed class PlatformBootstrap(AppDbContext db, UserManager<AppUser> users, IOnboardingService onboarding, IConfiguration config, ILogger<PlatformBootstrap> log)
{
    public const string InternalWorkspaceKey = "platform-hq";

    public async Task RunAsync(CancellationToken ct = default)
    {
        await SeedPlansAndSettingsAsync(ct);
        await EnsureFounderAsync(ct);
    }

    /// <summary>Placeholder prices for the founder to replace in the console; nothing is sold until registration is opened.</summary>
    private async Task SeedPlansAndSettingsAsync(CancellationToken ct)
    {
        if (!await db.SubscriptionPlans.AnyAsync(ct))
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan
                {
                    Code = "starter", Name = "Starter", Description = "For small teams getting organised.", SortOrder = 10, MonthlyPrice = 49, AnnualPrice = 490, TrialDays = 14, MaxUsers = 5, MaxLegalEntities = 1,
                    Features = "Up to 5 users\n1 legal entity\nEmail support",
                },
                new SubscriptionPlan
                {
                    Code = "business", Name = "Business", Description = "For growing companies with several departments.", SortOrder = 20, MonthlyPrice = 149, AnnualPrice = 1490, TrialDays = 14, MaxUsers = 25, MaxLegalEntities = 3, IsRecommended = true,
                    Features = "Up to 25 users\n3 legal entities\nPriority support",
                },
                new SubscriptionPlan
                {
                    Code = "enterprise", Name = "Enterprise", Description = "For groups that need no limits.", SortOrder = 30, MonthlyPrice = 399, AnnualPrice = 3990, TrialDays = 14,
                    Features = "Unlimited users\nUnlimited legal entities\nDedicated support",
                });
            await db.SaveChangesAsync(ct);
        }
        if (!await db.PlatformSettings.AnyAsync(ct))
        {
            var preferred = await db.SubscriptionPlans.OrderByDescending(p => p.IsRecommended).ThenBy(p => p.SortOrder).Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
            db.PlatformSettings.Add(new PlatformSetting { DefaultPlanId = preferred });
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureFounderAsync(CancellationToken ct)
    {
        var email = config["Platform:FounderEmail"]?.Trim();
        if (string.IsNullOrEmpty(email)) return;
        var password = config["Platform:FounderPassword"];

        try
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null && string.IsNullOrEmpty(password))
            {
                log.LogWarning("Platform:FounderEmail has no account yet. Set Platform:FounderPassword once to create it.");
                return;
            }
            if (!await db.Organizations.AnyAsync(o => o.IsInternal, ct))
            {
                var country = config["Platform:CountryCode"] ?? "AE";
                var currency = config["Platform:BaseCurrency"] ?? "AED";
                await onboarding.CreateWorkspaceAsync(
                    new OnboardingRequest(config["Platform:CompanyName"] ?? "Platform HQ", InternalWorkspaceKey, "generic", country, currency, "UTC", null, email, config["Platform:FounderName"] ?? "Founder", password ?? ""),
                    new OnboardingOptions(TrustExistingAccount: true, AdminMustChangePassword: true, IsInternal: true), ct);
            }
            user = await users.FindByEmailAsync(email);
            if (user is null) return;
            if (!user.IsPlatformAdmin)
            {
                user.IsPlatformAdmin = true;
                await users.UpdateAsync(user);
                log.LogInformation("{Email} is now the platform owner.", email);
            }
        }
        catch (Exception e) when (e is FieldValidationException or ConflictException)
        {
            // A weak password or a clash must not keep every client's workspace from starting.
            log.LogError(e, "Could not set up the platform owner account. Check Platform:FounderEmail and Platform:FounderPassword.");
        }
    }
}
