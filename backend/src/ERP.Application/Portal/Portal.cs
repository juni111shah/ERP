namespace ERP.Application.Portal;

public sealed record PortalSession(string AccessToken, int ExpiresIn, Guid PartyId, string PartyName, string Kind = "customer");

/// <summary>Email one-time-code sign-in for customers (tenants). Implemented in Infrastructure; the token it issues carries only the "portal_party" claim, no staff permissions.</summary>
public interface IPortalAuth
{
    /// <summary>Always completes silently, so the caller cannot learn whether an email is registered.</summary>
    Task RequestCodeAsync(string workspaceCode, string email, string? kind, CancellationToken ct);
    Task<PortalSession> VerifyCodeAsync(string workspaceCode, string email, string code, string? kind, CancellationToken ct);
}
