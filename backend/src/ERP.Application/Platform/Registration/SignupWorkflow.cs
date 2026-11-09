using System.Security.Cryptography;
using System.Text;
using ERP.Application.Abstractions;
using ERP.Domain.Organizations;
using ERP.Domain.Platform;

namespace ERP.Application.Platform;

/// <summary>The steps shared by the registration handlers: whether a workspace code is free, provisioning an accepted registration and telling the founder.</summary>
internal sealed class SignupWorkflow(
    ISignupRequestRepository signups, IReadRepository<Organization> organizations, ClientProvisioner provisioner, PlatformLog log, IPlatformMailer mailer)
{
    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
        { "admin", "api", "app", "auth", "billing", "founder", "help", "hq", "mail", "platform", "platform-hq", "portal", "root", "support", "system", "www" };
    public const int VerificationHours = 48;
    public const string ReviewMessage = "Thanks. We have your registration and will email you as soon as your workspace is ready.";

    public bool Verifies(PlatformSetting s) => s.RequireEmailVerification && mailer.IsEnabled;

    public async Task<bool> KeyTakenAsync(string key, string? ownEmail, CancellationToken ct)
        => ReservedKeys.Contains(key)
           || await organizations.AnyAsync(o => o.Code == key, ct)
           || await signups.AnyAsync(r => r.SignInKey == key && r.Email != ownEmail && (r.Status == SignupStatus.PendingEmail || r.Status == SignupStatus.PendingReview), ct);

    public async Task<SignupResult> ProvisionAsync(SignupRequest request, CancellationToken ct)
    {
        var result = await provisioner.ProvisionAsync(request, ct);
        request.Status = SignupStatus.Provisioned;
        request.ProvisionedOrgId = result.OrgId;
        request.PasswordHash = null;
        log.Add("Client created", result.OrgId, request.BusinessName, "From the public registration page.");
        await signups.SaveChangesAsync(ct);
        return new(SignupStatus.Provisioned, "Your workspace is ready. Sign in to start.", request.SignInKey);
    }

    public Task NotifyFounderAsync(PlatformSetting settings, SignupRequest request, CancellationToken ct)
        => mailer.SendAsync(settings.SupportEmail, $"New registration: {request.BusinessName}", $"{request.BusinessName} ({request.Email}) registered and is waiting for approval.\n\n{mailer.Link("/platform/signups")}", ct);

    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
