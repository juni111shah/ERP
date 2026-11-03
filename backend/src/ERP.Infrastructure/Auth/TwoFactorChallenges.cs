using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Auth;

/// <summary>Why a login challenge was issued: the user has to set up an authenticator, or has one and must enter a code.</summary>
public enum TwoFactorChallengePurpose { Setup = 1, Verify = 2 }

public sealed record TwoFactorChallenge(Guid UserId, bool RememberMe, TwoFactorChallengePurpose Purpose, DateTime ExpiresAt, string PasswordFingerprint);

/// <summary>
/// Short-lived, scoped tokens that stand between a correct password and a session when two-factor is needed. Protected with Data
/// Protection (encrypted and signed, so they cannot be forged or read), scoped to one purpose, expiring after
/// Auth:TwoFactor:ChallengeMinutes (checked against <see cref="IClock"/>), and bound to the password hash so a password change
/// voids them. They grant nothing else: they are not bearer tokens and no other endpoint accepts them.
/// </summary>
public sealed class TwoFactorChallenges(IDataProtectionProvider dataProtection, IClock clock, IOptions<AuthOptions> options)
{
    private IDataProtector Protector => dataProtection.CreateProtector("erp.2fa-challenge.v1");
    private int Minutes => Math.Max(1, options.Value.TwoFactor.ChallengeMinutes);

    public int LifetimeSeconds => Minutes * 60;

    public string Issue(Guid userId, string? passwordHash, bool rememberMe, TwoFactorChallengePurpose purpose)
    {
        var expires = new DateTimeOffset(DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc)).AddMinutes(Minutes).ToUnixTimeMilliseconds();
        return Protector.Protect($"{userId:N}|{(rememberMe ? 1 : 0)}|{(int)purpose}|{expires}|{Fingerprint(passwordHash)}");
    }

    /// <summary>The challenge, or null when the token is malformed, tampered with or expired.</summary>
    public TwoFactorChallenge? Read(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        string payload;
        try { payload = Protector.Unprotect(token.Trim()); }
        catch (Exception e) when (e is CryptographicException or FormatException) { return null; }
        var parts = payload.Split('|');
        if (parts.Length != 5 || !Guid.TryParseExact(parts[0], "N", out var userId) || !int.TryParse(parts[2], out var purpose)
            || !Enum.IsDefined((TwoFactorChallengePurpose)purpose) || !long.TryParse(parts[3], out var expiresMs)) return null;
        var expires = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs).UtcDateTime;
        if (DateTime.SpecifyKind(clock.UtcNow, DateTimeKind.Utc) >= expires) return null;
        return new(userId, parts[1] == "1", (TwoFactorChallengePurpose)purpose, expires, parts[4]);
    }

    public static string Fingerprint(string? passwordHash)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash ?? "")))[..16];
}
