using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Lockout on signed-in confirmations (TASK-100) and session revocation (TASK-101).</summary>
public class SessionSecurityTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Temp = "Temp#Passw0rd1";

    private async Task<(string Email, string UserId, HttpClient Admin)> NewUserAsync()
    {
        var email = $"sec{Guid.NewGuid():N}@demo.local"[..24] + "@demo.local";
        var admin = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var roles = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"))).AsArray();
        var viewer = (string)roles.First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var invite = await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Sec User", temporaryPassword = Temp, roleIds = new[] { viewer } });
        Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
        return (email, (string)(await ApiFactory.Json(invite))["id"]!, admin);
    }

    private static Task<HttpResponseMessage> Change(HttpClient c, string current) => c.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = current, newPassword = "Brand#New2026x" });

    [Fact]
    public async Task Wrong_current_passwords_lock_the_account_so_a_stolen_token_cannot_guess_it()
    {
        var (email, _, _) = await NewUserAsync();
        var (user, _) = await f.SignInAsync(email, Temp);
        for (var i = 0; i < 4; i++) Assert.Equal(HttpStatusCode.BadRequest, (await Change(user, "wrong-" + i)).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Change(user, "wrong-5")).StatusCode); // fifth failure locks
        Assert.Equal((HttpStatusCode)423, (await Change(user, Temp)).StatusCode);       // even the right one is refused while locked
        Assert.Equal((HttpStatusCode)423, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = Temp })).StatusCode);
    }

    [Fact]
    public async Task A_successful_change_resets_the_failure_counter()
    {
        var (email, _, _) = await NewUserAsync();
        var (user, _) = await f.SignInAsync(email, Temp);
        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.BadRequest, (await Change(user, "wrong-" + i)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Change(user, Temp)).StatusCode);
    }

    [Fact]
    public async Task A_wrong_two_factor_disable_attempt_counts_as_a_failed_access()
    {
        var (email, _, _) = await NewUserAsync();
        var (user, _) = await f.SignInAsync(email, Temp);
        for (var i = 0; i < 4; i++) Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/v1/auth/2fa/disable", new { password = "wrong-" + i, code = "000000" })).StatusCode);
        Assert.Equal((HttpStatusCode)423, (await user.PostAsJsonAsync("/api/v1/auth/2fa/disable", new { password = "wrong-5", code = "000000" })).StatusCode);
    }

    [Fact]
    public void Password_and_two_factor_confirmation_endpoints_are_rate_limited()
    {
        _ = f.CreateClient();
        var wanted = new[] { "auth/change-password", "auth/2fa/setup", "auth/2fa/enable", "auth/2fa/disable" };
        var endpoints = f.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToList();
        foreach (var route in wanted)
        {
            var e = endpoints.Single(x => x.RoutePattern.RawText!.EndsWith(route, StringComparison.OrdinalIgnoreCase));
            Assert.True(e.Metadata.GetMetadata<EnableRateLimitingAttribute>() is not null, $"{route} has no rate limiting policy");
        }
    }
}
