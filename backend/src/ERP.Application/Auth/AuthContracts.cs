namespace ERP.Application.Auth;

/// <summary>Authentication settings (bound from the "Auth" configuration section). Plain data: no infrastructure types.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";
    /// <summary>HMAC-SHA256 signing key, at least 32 bytes. Must come from configuration/secrets; never commit a real one.</summary>
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "erp-api";
    public string Audience { get; set; } = "erp-web";
    public int AccessTokenMinutes { get; set; } = 5;
    public int PersistentRefreshDays { get; set; } = 30;
    public int SessionRefreshHours { get; set; } = 12;
    public bool SecureCookies { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int ResetTokenMinutes { get; set; } = 30;
    public const string RefreshCookie = "erp_rt";
    public const string RefreshCookiePath = "/api/v1/auth";
}

public sealed record WorkspaceOption(Guid Id, string Code, string Name, string Industry);

public sealed record SessionResult(
    string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshExpiresAt, bool Persistent,
    WorkspaceOption Workspace, IReadOnlyList<WorkspaceOption> Workspaces);

public sealed record LoginResult(
    bool RequiresWorkspaceSelection, string? SelectionToken, IReadOnlyList<WorkspaceOption> Workspaces, SessionResult? Session, bool RequiresTwoFactor = false);

public sealed record TwoFactorSetup(string Secret, string OtpAuthUri);
public sealed record TwoFactorEnabled(IReadOnlyList<string> RecoveryCodes);
public sealed record TwoFactorStatus(bool Enabled);

/// <summary>Effective access of a user in one workspace.</summary>
public sealed record AccessGrant(IReadOnlySet<string> Permissions, IReadOnlyList<Guid>? Entities, IReadOnlySet<string> EnabledModules);

public sealed class AuthException(int status, string message, DateTime? retryAt = null) : Exception(message)
{
    public int Status { get; } = status;
    public DateTime? RetryAt { get; } = retryAt;
}

/// <summary>Sign-in, token and password operations. Implemented in Infrastructure (Identity, JWT, refresh tokens); called only by the auth commands.</summary>
public interface IAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, bool rememberMe, string? ip, string? twoFactorCode, CancellationToken ct);
    Task<TwoFactorStatus> TwoFactorStatusAsync(Guid userId, CancellationToken ct);
    Task<TwoFactorSetup> BeginTwoFactorSetupAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<string>> EnableTwoFactorAsync(Guid userId, string code, CancellationToken ct);
    Task DisableTwoFactorAsync(Guid userId, string password, string code, CancellationToken ct);
    Task<SessionResult> SelectWorkspaceAsync(string selectionToken, Guid orgId, string? ip, CancellationToken ct);
    Task<SessionResult> SwitchWorkspaceAsync(Guid userId, Guid currentOrgId, Guid orgId, string? currentJti, DateTime? currentExp, bool persistent, string? ip, CancellationToken ct);
    Task<SessionResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct);
    Task LogoutAsync(string? rawRefreshToken, string? jti, DateTime? exp, CancellationToken ct);
    Task ForgotPasswordAsync(string email, CancellationToken ct);
    Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceOption>> WorkspacesForAsync(Guid userId, CancellationToken ct);
    Task<AccessGrant> ResolveAccessAsync(Guid userId, Guid orgId, CancellationToken ct);
}
