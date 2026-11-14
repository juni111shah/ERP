using System.IdentityModel.Tokens.Jwt;
using ERP.Application.Auth;
using ERP.Modularity.Web;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ERP.Api.Controllers.Auth;

/// <summary>Sign-in, two-factor, sessions (refresh cookie), workspace switching and passwords.</summary>
[Route("api/v1/auth")]
public sealed class AuthController(ISender mediator) : ApiControllerBase(mediator)
{
    public const string ClientHeader = "X-ERP-Client";

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest r, [FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        var result = await Mediator.Send(new LoginCommand(r.Email, r.Password, r.RememberMe, Ip(), r.TwoFactorCode), ct);
        // Two-factor still to do: no session, only a short-lived challenge for the setup / enter-code step.
        // requiresTwoFactor stays for clients that send the code inline with the password (older app builds).
        if (result.RequiresTwoFactor)
            return Ok(new { requiresTwoFactor = true, twoFactorRequired = true, challengeToken = result.ChallengeToken, challengeExpiresIn = result.ChallengeExpiresIn });
        if (result.TwoFactorSetupRequired)
            return Ok(new { twoFactorSetupRequired = true, challengeToken = result.ChallengeToken, challengeExpiresIn = result.ChallengeExpiresIn });
        return SignedIn(result, o.Value);
    }

    // First sign-in two-factor setup and returning-user code entry, authorised by the challenge token from /login (not a session).
    [HttpPost("login/2fa/setup")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public Task<TwoFactorChallengeSetup> BeginChallengeSetup(ChallengeRequest r, CancellationToken ct)
        => Mediator.Send(new BeginTwoFactorChallengeSetupCommand(r.ChallengeToken), ct);

    [HttpPost("login/2fa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> VerifyChallenge(ChallengeCodeRequest r, [FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        var result = await Mediator.Send(new VerifyTwoFactorChallengeCommand(r.ChallengeToken, r.Code, Ip()), ct);
        return SignedIn(result.Login, o.Value, result.RecoveryCodes);
    }

    [HttpGet("2fa")]
    [SignedIn("The caller's own two-factor sign-in settings")]
    public Task<TwoFactorStatus> GetTwoFactorStatus(CancellationToken ct) => Mediator.Send(new GetTwoFactorStatusQuery(), ct);

    [HttpPost("2fa/setup")]
    [SignedIn("The caller's own two-factor sign-in settings")]
    [EnableRateLimiting("login")]
    public Task<TwoFactorSetup> BeginTwoFactorSetup(CancellationToken ct) => Mediator.Send(new BeginTwoFactorSetupCommand(), ct);

    [HttpPost("2fa/enable")]
    [SignedIn("The caller's own two-factor sign-in settings")]
    [EnableRateLimiting("login")]
    public Task<TwoFactorEnabled> EnableTwoFactor(TwoFactorCodeRequest r, CancellationToken ct) => Mediator.Send(new EnableTwoFactorCommand(r.Code), ct);

    [HttpPost("2fa/disable")]
    [SignedIn("The caller's own two-factor sign-in settings")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> DisableTwoFactor(DisableTwoFactorRequest r, CancellationToken ct)
    {
        await Mediator.Send(new DisableTwoFactorCommand(r.Password, r.Code), ct);
        return NoContent();
    }

    [HttpPost("select-workspace")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> SelectWorkspace(SelectWorkspaceRequest r, [FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        var s = await Mediator.Send(new SelectWorkspaceCommand(r.SelectionToken, r.WorkspaceId, Ip()), ct);
        SetCookie(s, o.Value);
        return Ok(SessionBody(s));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey(ClientHeader)) return Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
        try
        {
            var s = await Mediator.Send(new RefreshSessionCommand(Request.Cookies[AuthOptions.RefreshCookie], Ip()), ct);
            SetCookie(s, o.Value);
            return Ok(SessionBody(s));
        }
        catch (AuthException)
        {
            ClearCookie(o.Value);
            throw;
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        if (!Request.Headers.ContainsKey(ClientHeader)) return Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
        var (jti, exp) = TokenInfo();
        await Mediator.Send(new LogoutCommand(Request.Cookies[AuthOptions.RefreshCookie], jti, exp), ct);
        ClearCookie(o.Value);
        return NoContent();
    }

    [HttpPost("switch-workspace")]
    [SignedIn("Moves the caller's own session to another workspace they are a member of")]
    public async Task<IActionResult> SwitchWorkspace(SwitchWorkspaceRequest r, [FromServices] IOptions<AuthOptions> o, CancellationToken ct)
    {
        var (jti, exp) = TokenInfo();
        var persistent = User.FindFirst("rp")?.Value == "1";
        var s = await Mediator.Send(new SwitchWorkspaceCommand(r.WorkspaceId, persistent, jti, exp, Ip()), ct);
        SetCookie(s, o.Value);
        return Ok(SessionBody(s));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(ForgotRequest r, CancellationToken ct)
    {
        await Mediator.Send(new ForgotPasswordCommand(r.Email), ct);
        return Accepted(new { message = "If that email has an account, a reset link is on its way. It expires in 30 minutes." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetRequest r, CancellationToken ct)
    {
        await Mediator.Send(new ResetPasswordCommand(r.Token, r.NewPassword), ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [SignedIn("The caller's own password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest r, CancellationToken ct)
    {
        await Mediator.Send(new ChangePasswordCommand(r.CurrentPassword, r.NewPassword), ct);
        return NoContent();
    }

    /// <summary>The answer once every factor has passed: the session (refresh cookie set), or the workspace choice.</summary>
    private IActionResult SignedIn(LoginResult result, AuthOptions o, IReadOnlyList<string>? recoveryCodes = null)
    {
        if (result.Session is { } s)
        {
            SetCookie(s, o);
            return Ok(recoveryCodes is null ? SessionBody(s) : new
            {
                accessToken = s.AccessToken, tokenType = "Bearer", expiresIn = s.ExpiresIn, workspace = s.Workspace, workspaces = s.Workspaces, persistent = s.Persistent,
                recoveryCodes,
            });
        }
        return recoveryCodes is null
            ? Ok(new { requiresWorkspaceSelection = true, result.SelectionToken, workspaces = result.Workspaces })
            : Ok(new { requiresWorkspaceSelection = true, result.SelectionToken, workspaces = result.Workspaces, recoveryCodes });
    }

    private static object SessionBody(SessionResult s) => new
    {
        accessToken = s.AccessToken, tokenType = "Bearer", expiresIn = s.ExpiresIn, workspace = s.Workspace, workspaces = s.Workspaces, persistent = s.Persistent,
    };

    private void SetCookie(SessionResult s, AuthOptions o)
        => Response.Cookies.Append(AuthOptions.RefreshCookie, s.RefreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = o.SecureCookies, SameSite = SameSiteMode.Strict, Path = AuthOptions.RefreshCookiePath,
            Expires = s.Persistent ? s.RefreshExpiresAt : null, // session cookie unless "Keep me signed in"
            IsEssential = true,
        });

    private void ClearCookie(AuthOptions o)
        => Response.Cookies.Delete(AuthOptions.RefreshCookie, new CookieOptions { Path = AuthOptions.RefreshCookiePath, Secure = o.SecureCookies, SameSite = SameSiteMode.Strict, HttpOnly = true });

    private (string? Jti, DateTime? Exp) TokenInfo()
    {
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var exp = long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var e) ? DateTimeOffset.FromUnixTimeSeconds(e).UtcDateTime : (DateTime?)null;
        return (jti, exp);
    }

    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    public sealed record LoginRequest(string Email, string Password, bool RememberMe = false, string? TwoFactorCode = null);
    public sealed record TwoFactorCodeRequest(string Code);
    public sealed record DisableTwoFactorRequest(string Password, string Code);
    public sealed record ChallengeRequest(string ChallengeToken);
    public sealed record ChallengeCodeRequest(string ChallengeToken, string Code);
    public sealed record SelectWorkspaceRequest(string SelectionToken, Guid WorkspaceId);
    public sealed record SwitchWorkspaceRequest(Guid WorkspaceId);
    public sealed record ForgotRequest(string Email);
    public sealed record ResetRequest(string Token, string NewPassword);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
