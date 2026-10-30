using System.Net;
using System.Net.Http.Json;

namespace ERP.IntegrationTests;

/// <summary>TASK-102: portal sign-in codes are limited per person, not just per IP.</summary>
public class PortalCodeThrottleTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Portal = "/api/v1/portal";

    [Fact]
    public async Task The_sixth_code_request_within_an_hour_sends_nothing_and_keeps_the_fifth_code_valid()
    {
        var (staff, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var email = "thr" + Guid.NewGuid().ToString("N")[..8] + "@example.com";
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync("/api/v1/parties", new { name = "Throttle " + email[..8], kind = "Company", roles = "Customer", email })).StatusCode);

        string? fifth = null;
        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email })).StatusCode);
            fifth = f.Emails.LastCodeFor(email);
            Assert.NotNull(fifth);
        }
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email })).StatusCode); // same answer
        Assert.Equal(fifth, f.Emails.LastCodeFor(email)); // nothing new was sent

        var verify = await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email, code = fifth });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode); // the fifth code was not invalidated by a sixth
    }
}
