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
    /// <summary>
    /// Browser tabs share one refresh cookie, so a tab can send a token another tab replaced a moment ago. Inside this window that
    /// is treated as the overlap it is. After it, reuse counts as theft and ends the whole sign-in.
    /// </summary>
    public int RefreshReuseLeewaySeconds { get; set; } = 20;
    public bool SecureCookies { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int ResetTokenMinutes { get; set; } = 30;
    public TwoFactorOptions TwoFactor { get; set; } = new();
    public const string RefreshCookie = "erp_rt";
    public const string RefreshCookiePath = "/api/v1/auth";
}

/// <summary>
/// Two-factor sign-in ("Auth:TwoFactor"). With <see cref="Enforce"/> on (the default), a user without a confirmed authenticator
/// gets no session from email and password alone: login answers with a short-lived setup challenge, and the session is issued only
/// once the first code from the app is confirmed. Development turns it off in appsettings.Development.json.
/// </summary>
public sealed class TwoFactorOptions
{
    public bool Enforce { get; set; } = true;
    /// <summary>The name the authenticator app shows next to the account. Generic; set per deployment.</summary>
    public string Issuer { get; set; } = "ERP";
    /// <summary>How long a setup / verify challenge from login stays valid.</summary>
    public int ChallengeMinutes { get; set; } = 10;
}

public sealed record WorkspaceOption(Guid Id, string Code, string Name, string Industry);

public sealed record SessionResult(
    string AccessToken, int ExpiresIn, string RefreshToken, DateTime RefreshExpiresAt, bool Persistent,
    WorkspaceOption Workspace, IReadOnlyList<WorkspaceOption> Workspaces);

public sealed record LoginResult(
    bool RequiresWorkspaceSelection, string? SelectionToken, IReadOnlyList<WorkspaceOption> Workspaces, SessionResult? Session, bool RequiresTwoFactor = false,
    bool TwoFactorSetupRequired = false, string? ChallengeToken = null, int ChallengeExpiresIn = 0);

public sealed record TwoFactorSetup(string Secret, string OtpAuthUri, string? QrCodeDataUri = null);

/// <summary>What the first-sign-in setup step shows: the QR image of the otpauth:// link, and the key for typing in by hand.</summary>
public sealed record TwoFactorChallengeSetup(string OtpAuthUri, string Secret, string QrCodeDataUri, string Issuer, string Account, int Digits, int Period);

/// <summary>A confirmed challenge: the normal sign-in outcome, plus the one-time recovery codes when this confirmed a new setup.</summary>
public sealed record TwoFactorChallengeResult(LoginResult Login, IReadOnlyList<string>? RecoveryCodes);

/// <summary>Renders the authenticator QR code (implemented in Infrastructure).</summary>
public interface IQrCodeRenderer
{
    /// <summary>A data:image/png;base64 URI of <paramref name="text"/> as a QR code.</summary>
    string PngDataUri(string text);
}
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
    /// <summary>Setup step of a login challenge: creates the authenticator key and returns the QR / manual-entry details.</summary>
    Task<TwoFactorChallengeSetup> BeginChallengeSetupAsync(string challengeToken, CancellationToken ct);
    /// <summary>Confirms a login challenge with a code (a first code for a new setup, or a code / recovery code for a returning user) and signs in.</summary>
    Task<TwoFactorChallengeResult> VerifyChallengeAsync(string challengeToken, string code, string? ip, CancellationToken ct);
    Task<SessionResult> SelectWorkspaceAsync(string selectionToken, Guid orgId, string? ip, CancellationToken ct);
    Task<SessionResult> SwitchWorkspaceAsync(Guid userId, Guid currentOrgId, Guid orgId, string? currentJti, DateTime? currentExp, bool persistent, string? ip, CancellationToken ct);
    Task<SessionResult> RefreshAsync(string? rawRefreshToken, string? ip, CancellationToken ct);
    Task LogoutAsync(string? rawRefreshToken, string? jti, DateTime? exp, CancellationToken ct);
    Task ForgotPasswordAsync(string email, CancellationToken ct);
    Task ResetPasswordAsync(string rawToken, string newPassword, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct);
    Task<IReadOnlyList<WorkspaceOption>> WorkspacesForAsync(Guid userId, CancellationToken ct);
    Task<AccessGrant> ResolveAccessAsync(Guid userId, Guid orgId, CancellationToken ct);
    /// <summary>Ends the user's sessions at once: refresh tokens are revoked and access tokens issued before now are rejected. With an org id only that workspace is affected; without one, all of them.</summary>
    Task RevokeSessionsAsync(Guid userId, Guid? orgId, string reason, CancellationToken ct);
}
