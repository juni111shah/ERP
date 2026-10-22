using System.IdentityModel.Tokens.Jwt;
using ERP.Application.Abstractions;
using ERP.Application.Auth;
using MediatR;
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

        g.MapPost("/login", async (LoginRequest r, ISender sender, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            var result = await sender.Send(new Login(r.Email, r.Password, r.RememberMe, Ip(http)), ct);
            if (result.Session is { } s) { SetCookie(http, s, o.Value); return Results.Ok(SessionBody(s)); }
            return Results.Ok(new { requiresWorkspaceSelection = true, result.SelectionToken, workspaces = result.Workspaces });
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/select-workspace", async (SelectWorkspaceRequest r, ISender sender, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            var s = await sender.Send(new SelectWorkspace(r.SelectionToken, r.WorkspaceId, Ip(http)), ct);
            SetCookie(http, s, o.Value);
            return Results.Ok(SessionBody(s));
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/refresh", async (ISender sender, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(ClientHeader)) return Results.Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
            try
            {
                var s = await sender.Send(new RefreshSession(http.Request.Cookies[AuthOptions.RefreshCookie], Ip(http)), ct);
                SetCookie(http, s, o.Value);
                return Results.Ok(SessionBody(s));
            }
            catch (AuthException)
            {
                ClearCookie(http, o.Value);
                throw;
            }
        }).AllowAnonymous();

        g.MapPost("/logout", async (ISender sender, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            if (!http.Request.Headers.ContainsKey(ClientHeader)) return Results.Problem(statusCode: 400, title: $"Missing {ClientHeader} header");
            var (jti, exp) = TokenInfo(http);
            await sender.Send(new Logout(http.Request.Cookies[AuthOptions.RefreshCookie], jti, exp), ct);
            ClearCookie(http, o.Value);
            return Results.NoContent();
        }).AllowAnonymous();

        g.MapPost("/switch-workspace", async (SwitchWorkspaceRequest r, ISender sender, HttpContext http, IOptions<AuthOptions> o, CancellationToken ct) =>
        {
            var (jti, exp) = TokenInfo(http);
            var persistent = http.User.FindFirst("rp")?.Value == "1";
            var s = await sender.Send(new SwitchWorkspace(r.WorkspaceId, persistent, jti, exp, Ip(http)), ct);
            SetCookie(http, s, o.Value);
            return Results.Ok(SessionBody(s));
        }).RequireAuthorization();

        g.MapPost("/forgot-password", async (ForgotRequest r, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ForgotPassword(r.Email), ct);
            return Results.Accepted(value: new { message = "If that email has an account, a reset link is on its way. It expires in 30 minutes." });
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/reset-password", async (ResetRequest r, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ResetPassword(r.Token, r.NewPassword), ct);
            return Results.NoContent();
        }).AllowAnonymous().RequireRateLimiting("login");

        g.MapPost("/change-password", async (ChangePasswordRequest r, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new ChangePassword(r.CurrentPassword, r.NewPassword), ct);
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
