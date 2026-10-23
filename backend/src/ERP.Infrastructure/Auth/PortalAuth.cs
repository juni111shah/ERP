using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Application.Portal;
using ERP.Application.Reminders;
using ERP.Domain.Parties;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Auth;

/// <summary>
/// Tenant portal sign-in: a customer enters their email, receives a 6-digit code (10 minutes, 5 attempts) and gets a one-hour token that
/// carries "portal_party" and no staff permissions. Unknown emails and workspaces are indistinguishable from known ones.
/// </summary>
public sealed class PortalAuth(AppDbContext db, IOrgContext org, IEnumerable<IMessageChannel> channels, IClock clock, IOptions<AuthOptions> options) : IPortalAuth
{
    public const int CodeMinutes = 10, MaxAttempts = 5, TokenMinutes = 60;
    private readonly AuthOptions _o = options.Value;

    public async Task RequestCodeAsync(string workspaceCode, string email, CancellationToken ct)
    {
        var (orgId, party) = await FindAsync(workspaceCode, email, ct);
        if (party is null) return;
        using var _ = org.Use(orgId);
        var now = clock.UtcNow;
        foreach (var old in await db.PortalOtps.Where(x => x.PartyId == party.Id && x.UsedAt == null).ToListAsync(ct)) old.UsedAt = now;
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        db.PortalOtps.Add(new PortalOtp { OrgId = orgId, PartyId = party.Id, CodeHash = AuthService.Hash(code + party.Id), ExpiresAt = now.AddMinutes(CodeMinutes) });
        var subject = "Your sign-in code";
        db.MessageLogs.Add(new MessageLog { OrgId = orgId, Channel = "email", Recipient = email, Subject = subject, Body = "A sign-in code was sent.", DedupeKey = $"portal-otp:{Guid.NewGuid()}", SentAt = now, Status = MessageStatus.Sent });
        await db.SaveChangesAsync(ct);
        await channels.Single(c => c.Name == "email").SendAsync(new(email, subject, $"Your sign-in code is {code}. It is valid for {CodeMinutes} minutes.", null, "info"), ct);
    }

    public async Task<PortalSession> VerifyCodeAsync(string workspaceCode, string email, string code, CancellationToken ct)
    {
        var fail = new AuthException(401, "The code is invalid or has expired.");
        var (orgId, party) = await FindAsync(workspaceCode, email, ct);
        if (party is null) throw fail;
        using var _ = org.Use(orgId);
        var now = clock.UtcNow;
        var otp = await db.PortalOtps.Where(x => x.PartyId == party.Id && x.UsedAt == null && x.ExpiresAt > now).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (otp is null || otp.Attempts >= MaxAttempts) throw fail;
        otp.Attempts++;
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(otp.CodeHash), Encoding.UTF8.GetBytes(AuthService.Hash(code + party.Id)));
        if (ok) otp.UsedAt = now;
        await db.SaveChangesAsync(ct);
        if (!ok) throw fail;

        var expires = now.AddMinutes(TokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, party.Id.ToString()), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, party.Email ?? email), new("name", party.Name),
            new(ErpClaims.Org, orgId.ToString()), new("portal_party", party.Id.ToString()), new(ErpClaims.Entities, ErpClaims.AllEntities),
        };
        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(_o.Issuer, _o.Audience, claims, now, expires, creds));
        return new(token, TokenMinutes * 60, party.Id, party.Name);
    }

    private async Task<(Guid OrgId, Party? Party)> FindAsync(string workspaceCode, string email, CancellationToken ct)
    {
        var code = workspaceCode.Trim().ToLowerInvariant();
        var orgId = await db.Organizations.IgnoreQueryFilters().Where(o => o.IsActive && o.Code.ToLower() == code).Select(o => o.Id).FirstOrDefaultAsync(ct);
        if (orgId == Guid.Empty) return (Guid.Empty, null);
        using var _ = org.Use(orgId);
        var mail = email.Trim().ToLowerInvariant();
        var party = await db.Parties.Where(p => p.IsActive && p.Email != null && p.Email.ToLower() == mail && (p.Roles & PartyRoles.Customer) != 0).FirstOrDefaultAsync(ct);
        return (orgId, party);
    }
}
