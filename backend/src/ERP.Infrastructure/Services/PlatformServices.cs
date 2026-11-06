using System.Security.Cryptography;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Domain.Platform;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Services;

/// <summary>Sends platform email when a server is configured. A failed send is logged: billing and sign-ups never depend on it.</summary>
public sealed class PlatformMailer(IEmailSender email, IOptions<EmailOptions> options, ILogger<PlatformMailer> log) : IPlatformMailer
{
    public bool IsEnabled => email.IsEnabled;

    public string Link(string path) => options.Value.AppBaseUrl.TrimEnd('/') + path;

    public async Task SendAsync(string? to, string subject, string body, CancellationToken ct = default)
    {
        if (!email.IsEnabled || string.IsNullOrWhiteSpace(to)) return;
        try { await email.SendAsync(to, subject, body, null, ct); }
        catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Could not send platform email \"{Subject}\"", subject); }
    }
}

/// <summary>A workspace's standing, cached briefly because the gate asks on every request. Changes clear it at once.</summary>
public sealed class SubscriptionAccess(AppDbContext db, IMemoryCache cache, IClock clock) : ISubscriptionAccess
{
    private static string Key(Guid orgId) => $"org:{orgId}:subscription";

    public void Invalidate(Guid orgId) => cache.Remove(Key(orgId));

    public async Task<SubscriptionSummary> GetAsync(Guid orgId, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(Key(orgId), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            var s = await db.WorkspaceSubscriptions.AsNoTracking().FirstOrDefaultAsync(x => x.OrgId == orgId, ct);
            if (s is null) return SubscriptionSummary.Unrestricted;
            var settings = await db.PlatformSettings.AsNoTracking().OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct) ?? new PlatformSetting();
            var plan = await db.SubscriptionPlans.AsNoTracking().Where(p => p.Id == s.PlanId).Select(p => p.Name).FirstOrDefaultAsync(ct);
            var today = DateOnly.FromDateTime(clock.UtcNow);
            var left = s.Status == SubscriptionStatus.Trialing && s.TrialEndsOn is { } end ? (int?)Math.Max(0, end.DayNumber - today.DayNumber) : null;
            return new SubscriptionSummary(true, s.Status, SubscriptionRules.AccessOf(s, settings, today), plan, s.TrialEndsOn, left, s.CurrentPeriodEnd, s.LapsedOn, s.IsComplimentary, s.Status == SubscriptionStatus.Cancelled ? s.CancelReason : s.SuspendedReason);
        }) ?? SubscriptionSummary.Unrestricted;
}

/// <summary>The platform console's route across workspaces. This class and <see cref="SubscriptionAccess"/> are why the console needs no bypass elsewhere.</summary>
public sealed class PlatformDirectory(AppDbContext db, UserManager<AppUser> users, IOrgContext org, IMemoryCache cache, IClock clock) : IPlatformDirectory
{
    public Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken ct = default)
        => db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsPlatformAdmin && u.IsActive, ct);

    public async Task<IReadOnlyDictionary<Guid, WorkspaceUsage>> UsageAsync(IReadOnlyCollection<Guid>? orgIds = null, CancellationToken ct = default)
    {
        var members = await db.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.IsActive && (orgIds == null || orgIds.Contains(m.OrgId)))
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.OrgId, u.LastSignInAt })
            .ToListAsync(ct);
        var entities = await db.LegalEntities.IgnoreQueryFilters().AsNoTracking()
            .Where(e => !e.IsDeleted && (orgIds == null || orgIds.Contains(e.OrgId)))
            .GroupBy(e => e.OrgId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var modules = (await db.OrganizationModules.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.IsEnabled && (orgIds == null || orgIds.Contains(m.OrgId))).Select(m => new { m.OrgId, m.ModuleKey }).ToListAsync(ct))
            .GroupBy(m => m.OrgId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.ModuleKey).OrderBy(x => x).ToList());
        var grouped = members.GroupBy(m => m.OrgId).ToDictionary(g => g.Key, g => g.ToList());
        return grouped.Keys.Concat(entities.Keys).Concat(modules.Keys).Distinct().ToDictionary(id => id, id =>
        {
            var list = grouped.GetValueOrDefault(id) ?? [];
            return new WorkspaceUsage(id, list.Count, entities.GetValueOrDefault(id), modules.GetValueOrDefault(id) ?? [], list.Max(m => m.LastSignInAt));
        });
    }

    public async Task<IReadOnlyList<WorkspaceMemberView>> MembersAsync(Guid orgId, CancellationToken ct = default)
    {
        var admins = await db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(ur => ur.OrgId == orgId)
            .Join(db.RolePermissions.AsNoTracking().IgnoreQueryFilters().Where(p => p.Permission == CorePermissions.All), ur => ur.RoleId, p => p.RoleId, (ur, _) => ur.UserId)
            .Distinct().ToListAsync(ct);
        var members = await db.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.OrgId == orgId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.Email, u.FullName, m.IsActive, u.LastSignInAt, u.TwoFactorEnabled })
            .ToListAsync(ct);
        return members.OrderBy(m => m.FullName)
            .Select(m => new WorkspaceMemberView(m.Id, m.Email ?? "", m.FullName, m.IsActive, admins.Contains(m.Id), m.LastSignInAt, m.TwoFactorEnabled)).ToList();
    }

    public async Task<string?> PrepareSignupPasswordAsync(string email, string password, CancellationToken ct = default)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
        {
            // The same lockout as signing in, so this form is no way to keep guessing a password.
            const string wrong = "This email already has an account. Enter that account's password to add a new workspace to it.";
            if (await users.IsLockedOutAsync(existing)) throw new FieldValidationException("email", wrong);
            if (!await users.CheckPasswordAsync(existing, password))
            {
                await users.AccessFailedAsync(existing);
                throw new FieldValidationException("email", wrong);
            }
            await users.ResetAccessFailedCountAsync(existing);
            return null;
        }
        var candidate = new AppUser { UserName = email, Email = email };
        var problems = new List<string>();
        foreach (var validator in users.PasswordValidators)
        {
            var result = await validator.ValidateAsync(users, candidate, password);
            if (!result.Succeeded) problems.AddRange(result.Errors.Select(e => e.Description));
        }
        if (problems.Count > 0) throw new FieldValidationException(new Dictionary<string, string[]> { ["password"] = problems.ToArray() });
        return users.PasswordHasher.HashPassword(candidate, password);
    }

    /// <summary>Random, readable and always passes the password rules (upper, lower, digit, symbol).</summary>
    public string NewTemporaryPassword()
    {
        const string letters = "abcdefghjkmnpqrstuvwxyz", capitals = "ABCDEFGHJKLMNPQRSTUVWXYZ", digits = "23456789";
        string Pick(string from, int count) => new(Enumerable.Range(0, count).Select(_ => from[RandomNumberGenerator.GetInt32(from.Length)]).ToArray());
        return $"{Pick(capitals, 3)}-{Pick(letters, 5)}-{Pick(digits, 4)}#";
    }

    public async Task<string> IssueTemporaryPasswordAsync(Guid orgId, Guid userId, CancellationToken ct = default)
    {
        if (!await db.OrganizationMembers.IgnoreQueryFilters().AnyAsync(m => m.OrgId == orgId && m.UserId == userId, ct)) throw new NotFoundException();
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException();
        if (user.IsPlatformAdmin) throw new ConflictException("A platform team member's password cannot be reset from here.");
        var password = NewTemporaryPassword();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded) throw new ConflictException(string.Join(" ", result.Errors.Select(e => e.Description)));
        user.MustChangePassword = true;
        await users.UpdateAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return password;
    }

    public async Task DisableModulesOutsideAsync(Guid orgId, IReadOnlyCollection<string> allowed, CancellationToken ct = default)
    {
        var rows = await db.OrganizationModules.IgnoreQueryFilters().Where(m => m.OrgId == orgId && m.IsEnabled && !allowed.Contains(m.ModuleKey)).ToListAsync(ct);
        foreach (var row in rows) row.IsEnabled = false;
        // A write for a different workspace needs that workspace's context.
        using (org.Use(orgId)) await db.SaveChangesAsync(ct);
        cache.Remove(ModuleAccess.CacheKey(orgId));
    }

    public async Task<IReadOnlyList<PlatformTeamMember>> TeamAsync(CancellationToken ct = default)
        => await db.Users.AsNoTracking().Where(u => u.IsPlatformAdmin).OrderBy(u => u.FullName)
            .Select(u => new PlatformTeamMember(u.Id, u.Email ?? "", u.FullName, u.LastSignInAt, u.TwoFactorEnabled)).ToListAsync(ct);

    public async Task<NewTeamMember> AddTeamMemberAsync(string email, string fullName, CancellationToken ct = default)
    {
        var user = await users.FindByEmailAsync(email);
        string? temporary = null;
        if (user is null)
        {
            var hq = await db.Organizations.AsNoTracking().Where(o => o.IsInternal).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct)
                ?? throw new ConflictException("Set Platform:FounderEmail first: the platform's own workspace does not exist yet.");
            temporary = NewTemporaryPassword();
            user = new AppUser { Id = ERP.Domain.Common.SequentialGuid.NewGuid(), UserName = email, Email = email, FullName = fullName, EmailConfirmed = true, MustChangePassword = true, CreatedAt = clock.UtcNow };
            var created = await users.CreateAsync(user, temporary);
            if (!created.Succeeded) throw new ConflictException(string.Join(" ", created.Errors.Select(e => e.Description)));
            db.OrganizationMembers.Add(new ERP.Domain.Identity.OrganizationMember { OrgId = hq, UserId = user.Id, JoinedAt = clock.UtcNow });
            using (org.Use(hq)) await db.SaveChangesAsync(ct);
        }
        user.IsPlatformAdmin = true;
        await users.UpdateAsync(user);
        return new(new(user.Id, user.Email ?? email, user.FullName, user.LastSignInAt, user.TwoFactorEnabled), temporary);
    }

    public async Task RemoveTeamMemberAsync(Guid userId, Guid actingUserId, CancellationToken ct = default)
    {
        if (userId == actingUserId) throw new ConflictException("You cannot remove yourself from the platform team.");
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException();
        user.IsPlatformAdmin = false;
        await users.UpdateAsync(user);
    }
}
