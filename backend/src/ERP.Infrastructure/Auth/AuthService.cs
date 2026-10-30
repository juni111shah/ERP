using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Identity;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using ERP.Modularity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Auth;

/// <summary>Password reset delivery (email in production; logged in development; captured in tests).</summary>
public interface IPasswordResetSender { Task SendAsync(string email, string token, CancellationToken ct = default); }

public sealed class LoggingPasswordResetSender(ILogger<LoggingPasswordResetSender> log, IEmailSender email, IOptions<EmailOptions> options) : IPasswordResetSender
{
    public async Task SendAsync(string to, string token, CancellationToken ct = default)
    {
        var link = $"{options.Value.AppBaseUrl.TrimEnd('/')}/auth/reset-password?token={Uri.EscapeDataString(token)}";
        if (email.IsEnabled)
        {
            try
            {
                await email.SendAsync(to, "Reset your ERP password", $"We received a request to reset your password.\n\nOpen this link to choose a new one (valid for a short time):\n{link}\n\nIf you did not ask for this, ignore this email.", null, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException) { log.LogError(e, "Could not email the password reset link to {Email}", to); } // never reveal failures to the caller
            return;
        }
        log.LogInformation("Password reset requested for {Email}. Dev-only link: {Link}", to, link);
    }
}

public sealed class AuthService(
    AppDbContext db,
    UserManager<AppUser> users,
    IOrgContext org,
    IPermissionCatalog catalog,
    ModuleRegistry modules,
    IDistributedCache cache,
    IDataProtectionProvider dataProtection,
    IPasswordResetSender resetSender,
    IClock clock,
    IOptions<AuthOptions> options) : IAuthService
{
    private readonly AuthOptions _o = options.Value;
    private static string? _dummyHash;
    public const string GenericFailure = "Invalid email or password.";

    private ITimeLimitedDataProtector SelectionProtector => dataProtection.CreateProtector("erp.workspace-selection").ToTimeLimitedDataProtector();

    public async Task<LoginResult> LoginAsync(string email, string password, bool rememberMe, string? ip, string? twoFactorCode, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            // Same work as a real check, so timing does not reveal whether the email exists.
            _dummyHash ??= users.PasswordHasher.HashPassword(new AppUser(), "timing-equaliser");
            users.PasswordHasher.VerifyHashedPassword(new AppUser(), _dummyHash, password);
            throw new AuthException(401, GenericFailure);
        }
        if (await users.IsLockedOutAsync(user)) throw Locked(user);

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            if (await users.IsLockedOutAsync(user)) throw Locked(user);
            throw new AuthException(401, GenericFailure);
        }
        if (!user.IsActive) throw new AuthException(401, GenericFailure);
        if (user.TwoFactorEnabled)
        {
            if (string.IsNullOrWhiteSpace(twoFactorCode)) return new(false, null, [], null, RequiresTwoFactor: true);
            if (!await VerifyTwoFactorAsync(user, twoFactorCode))
            {
                await users.AccessFailedAsync(user);
                if (await users.IsLockedOutAsync(user)) throw Locked(user);
                throw new AuthException(401, "The verification code is incorrect.");
            }
        }
        await users.ResetAccessFailedCountAsync(user);

        var workspaces = await WorkspacesForAsync(user.Id, ct);
        if (workspaces.Count == 0) throw new AuthException(403, "Your account is not a member of any active workspace.");

        user.LastSignInAt = clock.UtcNow;
        await users.UpdateAsync(user);

        if (workspaces.Count == 1)
            return new(false, null, workspaces, await IssueAsync(user, workspaces[0], workspaces, rememberMe, null, ip, ct));

        var selection = SelectionProtector.Protect($"{user.Id}|{(rememberMe ? 1 : 0)}", TimeSpan.FromMinutes(5));
        return new(true, selection, workspaces, null);
    }

    private async Task<bool> VerifyTwoFactorAsync(AppUser user, string code)
    {
        var clean = code.Replace(" ", "").Replace("-", "");
        if (await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, clean)) return true;
        return (await users.RedeemTwoFactorRecoveryCodeAsync(user, code.Trim())).Succeeded; // recovery codes keep their dash
    }

    public async Task<TwoFactorStatus> TwoFactorStatusAsync(Guid userId, CancellationToken ct)
        => new((await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, "Not signed in.")).TwoFactorEnabled);

    public async Task<TwoFactorSetup> BeginTwoFactorSetupAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, "Not signed in.");
        if (user.TwoFactorEnabled) throw new ConflictException("Two-factor authentication is already on. Turn it off first to set it up again.");
        await users.ResetAuthenticatorKeyAsync(user);
        var key = await users.GetAuthenticatorKeyAsync(user) ?? throw new InvalidOperationException("Could not create an authenticator key.");
        var uri = $"otpauth://totp/{Uri.EscapeDataString("ERP")}:{Uri.EscapeDataString(user.Email ?? user.UserName ?? "user")}?secret={key}&issuer={Uri.EscapeDataString("ERP")}&digits=6";
        return new(string.Join(' ', Enumerable.Range(0, (key.Length + 3) / 4).Select(i => key.Substring(i * 4, Math.Min(4, key.Length - i * 4)))), uri);
    }

    public async Task<IReadOnlyList<string>> EnableTwoFactorAsync(Guid userId, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, "Not signed in.");
        if (user.TwoFactorEnabled) throw new ConflictException("Two-factor authentication is already on.");
        if (await users.GetAuthenticatorKeyAsync(user) is null) throw new ConflictException("Start the setup first.");
        await EnsureNotLockedAsync(user);
        if (!await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code.Replace(" ", "").Replace("-", "")))
        {
            await CountFailureAsync(user);
            throw new FieldValidationException("code", "The code is incorrect. Check the time on your phone and try the next code.");
        }
        await users.ResetAccessFailedCountAsync(user);
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8))!.ToList();
    }

    public async Task DisableTwoFactorAsync(Guid userId, string password, string code, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, "Not signed in.");
        await EnsureNotLockedAsync(user);
        if (!await users.CheckPasswordAsync(user, password)) { await CountFailureAsync(user); throw new FieldValidationException("password", "The password is incorrect."); }
        if (user.TwoFactorEnabled && !await VerifyTwoFactorAsync(user, code)) { await CountFailureAsync(user); throw new FieldValidationException("code", "The code is incorrect."); }
        await users.ResetAccessFailedCountAsync(user);
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
    }

    /// <summary>A wrong password or code on a signed-in confirmation counts like a failed sign-in, so a stolen token cannot be used to guess either.</summary>
    private async Task CountFailureAsync(AppUser user)
    {
        await users.AccessFailedAsync(user);
        if (await users.IsLockedOutAsync(user)) throw Locked(user);
    }

    private async Task EnsureNotLockedAsync(AppUser user)
    {
        if (await users.IsLockedOutAsync(user)) throw Locked(user);
    }

    private AuthException Locked(AppUser user)
    {
        var until = user.LockoutEnd?.UtcDateTime ?? clock.UtcNow.AddMinutes(_o.LockoutMinutes);
        var minutes = Math.Max(1, (int)Math.Ceiling((until - clock.UtcNow).TotalMinutes));
        return new AuthException(423, $"Too many failed sign-in attempts. Try again in {minutes} minute(s), after {until:HH:mm} UTC.", until);
    }

    public async Task<SessionResult> SelectWorkspaceAsync(string selectionToken, Guid orgId, string? ip, CancellationToken ct)
    {
        string payload;
        try { payload = SelectionProtector.Unprotect(selectionToken); }
        catch (CryptographicException) { throw new AuthException(401, "Your sign-in has expired. Please sign in again."); }
        var parts = payload.Split('|');
        var user = await users.FindByIdAsync(parts[0]) ?? throw new AuthException(401, GenericFailure);
        if (!user.IsActive) throw new AuthException(401, GenericFailure);
        var workspaces = await WorkspacesForAsync(user.Id, ct);
        var ws = workspaces.FirstOrDefault(w => w.Id == orgId) ?? throw new AuthException(403, "You are not a member of that workspace.");
        return await IssueAsync(user, ws, workspaces, parts[1] == "1", null, ip, ct);
    }

    public async Task<SessionResult> SwitchWorkspaceAsync(Guid userId, Guid currentOrgId, Guid orgId, string? currentJti, DateTime? currentExp, bool persistent, string? ip, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, GenericFailure);
        var workspaces = await WorkspacesForAsync(user.Id, ct);
        var ws = workspaces.FirstOrDefault(w => w.Id == orgId) ?? throw new AuthException(403, "You are not a member of that workspace.");
        await DenyAsync(currentJti, currentExp, ct);
        // Leaving a workspace ends every session of this user in it.
        await db.RefreshTokens.Where(t => t.UserId == userId && t.OrgId == currentOrgId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow).SetProperty(t => t.RevokedReason, "workspace-switched"), ct);
        return await IssueAsync(user, ws, workspaces, persistent, null, ip, ct);
    }

    public async Task<SessionResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) throw new AuthException(401, "Not signed in.");
        var hash = Hash(rawRefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct) ?? throw new AuthException(401, "Not signed in.");
        var now = clock.UtcNow;
        if (token.RevokedAt is not null || token.ReplacedById is not null)
        {
            // Reuse of a rotated token: assume theft and revoke the whole family.
            await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now).SetProperty(t => t.RevokedReason, "reuse-detected"), ct);
            throw new AuthException(401, "Your session has ended. Please sign in again.");
        }
        if (token.ExpiresAt <= now) throw new AuthException(401, "Your session has expired. Please sign in again.");

        var user = await users.FindByIdAsync(token.UserId.ToString());
        if (user is null || !user.IsActive) throw new AuthException(401, "Not signed in.");
        var workspaces = await WorkspacesForAsync(user.Id, ct);
        var ws = workspaces.FirstOrDefault(w => w.Id == token.OrgId) ?? throw new AuthException(401, "Not signed in.");

        var session = await IssueAsync(user, ws, workspaces, token.IsPersistent, token, ip, ct);
        return session;
    }

    public async Task LogoutAsync(string? rawRefreshToken, string? jti, DateTime? exp, CancellationToken ct)
    {
        await DenyAsync(jti, exp, ct);
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;
        var hash = Hash(rawRefreshToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null) return;
        await db.RefreshTokens.Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow).SetProperty(t => t.RevokedReason, "logout"), ct);
    }

    public static string DenyKey(string jti) => $"jwt:deny:{jti}";

    private async Task DenyAsync(string? jti, DateTime? exp, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(jti)) return;
        var ttl = (exp ?? clock.UtcNow.AddMinutes(_o.AccessTokenMinutes)) - clock.UtcNow + TimeSpan.FromMinutes(1);
        if (ttl <= TimeSpan.Zero) return;
        await cache.SetStringAsync(DenyKey(jti), "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive) return; // no enumeration: the endpoint always answers 202
        var raw = NewToken();
        db.PasswordResetTokens.Add(new PasswordResetToken { UserId = user.Id, TokenHash = Hash(raw), CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddMinutes(_o.ResetTokenMinutes) });
        await db.SaveChangesAsync(ct);
        await resetSender.SendAsync(user.Email!, raw, ct);
    }

    public async Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct)
    {
        var hash = Hash(rawToken ?? "");
        var token = await db.PasswordResetTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.UsedAt is not null || token.ExpiresAt <= clock.UtcNow)
            throw new AuthException(400, "This reset link is invalid or has expired. Request a new one.");
        var user = await users.FindByIdAsync(token.UserId.ToString()) ?? throw new AuthException(400, "This reset link is invalid or has expired.");
        await SetPasswordAsync(user, newPassword);
        token.UsedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await RevokeAllAsync(user.Id, "password-reset", ct);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw new AuthException(401, "Not signed in.");
        await EnsureNotLockedAsync(user);
        if (!await users.CheckPasswordAsync(user, currentPassword))
        {
            await CountFailureAsync(user);
            throw new FieldValidationException("currentPassword", "The current password is incorrect.");
        }
        await users.ResetAccessFailedCountAsync(user);
        await SetPasswordAsync(user, newPassword);
        await RevokeAllAsync(user.Id, "password-changed", ct);
    }

    private async Task SetPasswordAsync(AppUser user, string newPassword)
    {
        var errors = new List<string>();
        foreach (var v in users.PasswordValidators)
        {
            var r = await v.ValidateAsync(users, user, newPassword);
            if (!r.Succeeded) errors.AddRange(r.Errors.Select(e => e.Description));
        }
        if (errors.Count > 0) throw new FieldValidationException(new Dictionary<string, string[]> { ["newPassword"] = errors.ToArray() });
        user.PasswordHash = users.PasswordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await users.UpdateSecurityStampAsync(user);
    }

    private Task RevokeAllAsync(Guid userId, string reason, CancellationToken ct)
        => db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, clock.UtcNow).SetProperty(t => t.RevokedReason, reason), ct);

    /// <summary>Active workspaces of a user. Membership is looked up across workspaces on purpose (explicit UserId predicate).</summary>
    public async Task<IReadOnlyList<WorkspaceOption>> WorkspacesForAsync(Guid userId, CancellationToken ct)
        => await (from m in db.OrganizationMembers.IgnoreQueryFilters() // allow-listed: login needs all memberships of this user
                  join o in db.Organizations on m.OrgId equals o.Id
                  where m.UserId == userId && m.IsActive && o.IsActive
                  orderby o.Name
                  select new WorkspaceOption(o.Id, o.Code, o.Name, o.IndustryCode)).ToListAsync(ct);

    /// <summary>Effective permissions in a workspace: role grants, "*" expanded, minus permissions of disabled modules.</summary>
    public async Task<AccessGrant> ResolveAccessAsync(Guid userId, Guid orgId, CancellationToken ct)
    {
        using var _ = org.Use(orgId);
        var granted = await (from ur in db.UserRoles
                             join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                             where ur.UserId == userId
                             select rp.Permission).Distinct().ToListAsync(ct);
        var enabled = (await db.OrganizationModules.Where(m => m.IsEnabled).Select(m => m.ModuleKey).ToListAsync(ct))
            .Where(modules.IsInstalled).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var perms = catalog.Expand(granted)
            .Where(p => { var mod = p.Split('.')[0]; return mod is "core" or "finance" || enabled.Contains(mod); })
            .ToHashSet(StringComparer.Ordinal);
        var ents = await db.UserLegalEntityAccess.Where(a => a.UserId == userId).Select(a => a.LegalEntityId).ToListAsync(ct);
        return new AccessGrant(perms, ents.Count == 0 ? null : ents, enabled);
    }

    private async Task<SessionResult> IssueAsync(AppUser user, WorkspaceOption ws, IReadOnlyList<WorkspaceOption> all, bool persistent, RefreshToken? rotateFrom, string? ip, CancellationToken ct)
    {
        var (perms, ents, _) = await ResolveAccessAsync(user.Id, ws.Id, ct);
        var now = clock.UtcNow;
        var expires = now.AddMinutes(_o.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("name", user.FullName),
            new(ErpClaims.Org, ws.Id.ToString()),
            new("rp", persistent ? "1" : "0"),
        };
        claims.AddRange(perms.Select(p => new Claim(ErpClaims.Permission, p)));
        if (ents is null) claims.Add(new Claim(ErpClaims.Entities, ErpClaims.AllEntities));
        else claims.AddRange(ents.Select(e => new Claim(ErpClaims.Entities, e.ToString())));

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_o.Issuer, _o.Audience, claims, now, expires, creds);
        var access = new JwtSecurityTokenHandler().WriteToken(jwt);

        var raw = NewToken();
        var refresh = new RefreshToken
        {
            UserId = user.Id, OrgId = ws.Id, TokenHash = Hash(raw), FamilyId = rotateFrom?.FamilyId ?? Guid.NewGuid(), IsPersistent = persistent,
            CreatedAt = now, ExpiresAt = rotateFrom?.ExpiresAt ?? (persistent ? now.AddDays(_o.PersistentRefreshDays) : now.AddHours(_o.SessionRefreshHours)),
            CreatedByIp = ip,
        };
        db.RefreshTokens.Add(refresh);
        if (rotateFrom is not null) { rotateFrom.ReplacedById = refresh.Id; rotateFrom.RevokedAt = now; rotateFrom.RevokedReason = "rotated"; }
        using (org.Use(ws.Id)) await db.SaveChangesAsync(ct);

        return new(access, _o.AccessTokenMinutes * 60, raw, refresh.ExpiresAt, persistent, ws, all);
    }

    private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
