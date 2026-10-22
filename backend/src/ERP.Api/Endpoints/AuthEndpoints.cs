using System.IdentityModel.Tokens.Jwt;
using ERP.Application.Abstractions;
using ERP.Infrastructure.Auth;
using ERP.Infrastructure.Identity;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ERP.Api.Endpoints;

public static class AuthEndpoints
{
    public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
    public sealed record SelectWorkspaceRequest(string SelectionToken, Guid WorkspaceId);
    public sealed record SwitchWorkspaceRequest(Guid WorkspaceId);
    public sealed record ForgotRequest(string Email);
    public sealed record ResetRequest(string Token, string NewPassword);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public const string ClientHeader = "X-ERP-Client";

    public static void MapAuth(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/auth").WithTags("Auth");

        g.MapPost("/login", async (LoginRequest r, AuthService auth, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email and password are required."] });
            var result = await auth.LoginAsync(r.Email, r.Password, r.RememberMe, Ip(http), ct);
            if (result.Session is { } s) { SetCookie(http, s, o.Value); return Results.Ok(SessionBody(s)); }
            return Results.Ok(new { requiresWorkspaceSelection = true, result.SelectionToken, workspaces = result.Workspaces });
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/select-workspace", async (SelectWorkspaceRequest r, AuthService auth, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            var s = await auth.SelectWorkspaceAsync(r.SelectionToken, r.WorkspaceId, Ip(http), ct);
            SetCookie(http, s, o.Value);
            return Results.Ok(SessionBody(s));
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/refresh", async (AuthService auth, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(ClientHeader)) return Results.Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
            try
            {
                var s = await auth.RefreshAsync(http.Request.Cookies[AuthOptions.RefreshCookie], Ip(http), ct);
                SetCookie(http, s, o.Value);
                return Results.Ok(SessionBody(s));
            }
            catch (AuthException)
            {
                ClearCookie(http, o.Value);
                throw;
            }
        }).AllowAnonymous();

        g.MapPost("/logout", async (AuthService auth, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(ClientHeader)) return Results.Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
            var (jti, exp) = TokenInfo(http);
            await auth.LogoutAsync(http.Request.Cookies[AuthOptions.RefreshCookie], jti, exp, ct);
            ClearCookie(http, o.Value);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapPost("/switch-workspace", async (SwitchWorkspaceRequest r, AuthService auth, ICurrentUser user, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            var (jti, exp) = TokenInfo(http);
            var persistent = http.User.FindFirst("rp")?.Value == "1";
            var s = await auth.SwitchWorkspaceAsync(user.UserId!.Value, user.OrgId!.Value, r.WorkspaceId, jti, exp, persistent, Ip(http), ct);
            SetCookie(http, s, o.Value);
            return Results.Ok(SessionBody(s));
        }).RequireAuthorization();

        g.MapPost("/forgot-password", async (ForgotRequest r, AuthService auth, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(r.Email)) await auth.ForgotPasswordAsync(r.Email, ct);
            return Results.Accepted(value: new { message = "If that email has an account, a reset link is on its way. It expires in 30 minutes." });
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/reset-password", async (ResetRequest r, AuthService auth, CancellationToken ct) =>
        {
            await auth.ResetPasswordAsync(r.Token, r.NewPassword, ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/change-password", async (ChangePasswordRequest r, AuthService auth, ICurrentUser user, CancellationToken ct) =>
        {
            await auth.ChangePasswordAsync(user.UserId!.Value, r.CurrentPassword, r.NewPassword, ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static object SessionBody(SessionResult s) => new
    {
        accessToken = s.AccessToken, tokenType = "Bearer", expiresIn = s.ExpiresIn, workspace = s.Workspace, workspaces = s.Workspaces, persistent = s.Persistent,
    };

    private static void SetCookie(HttpContext http, SessionResult s, AuthOptions o)
        => http.Response.Cookies.Append(AuthOptions.RefreshCookie, s.RefreshToken, new CookieOptions
        {
            HttpOnly = true, Secure = o.SecureCookies, SameSite = SameSiteMode.Strict, Path = AuthOptions.RefreshCookiePath,
            Expires = s.Persistent ? s.RefreshExpiresAt : null, // session cookie unless "Keep me signed in"
            IsEssential = true,
        });

    private static void ClearCookie(HttpContext http, AuthOptions o)
        => http.Response.Cookies.Delete(AuthOptions.RefreshCookie, new CookieOptions { Path = AuthOptions.RefreshCookiePath, Secure = o.SecureCookies, SameSite = SameSiteMode.Strict, HttpOnly = true });

    private static (string? Jti, DateTime? Exp) TokenInfo(HttpContext http)
    {
        var jti = http.User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var exp = long.TryParse(http.User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value, out var e) ? DateTimeOffset.FromUnixTimeSeconds(e).UtcDateTime : (DateTime?)null;
        return (jti, exp);
    }

    private static string? Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString();
}
