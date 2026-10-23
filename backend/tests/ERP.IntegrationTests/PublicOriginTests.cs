using System.Net;
using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ERP.IntegrationTests;

/// <summary>Hosting:PublicUrl (named-tunnel hostname) is added to CORS and used for email links; unset changes nothing.</summary>
public class PublicOriginTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Public = "https://erp-staging.example.test";

    private static HttpRequestMessage Preflight(string origin)
    {
        var req = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        req.Headers.Add("Origin", origin);
        req.Headers.Add("Access-Control-Request-Method", "POST");
        req.Headers.Add("Access-Control-Request-Headers", "content-type");
        return req;
    }

    [Fact]
    public async Task Public_origin_is_allowed_by_cors_and_used_for_email_links()
    {
        using var app = f.WithWebHostBuilder(b => b.UseSetting("Hosting:PublicUrl", Public + "/"));
        var client = app.CreateClient();

        var ok = await client.SendAsync(Preflight(Public));
        Assert.Equal(Public, ok.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", ok.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        var other = await client.SendAsync(Preflight("https://evil.example.test"));
        Assert.False(other.Headers.Contains("Access-Control-Allow-Origin"));

        Assert.Equal(Public, app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.AppBaseUrl);
    }

    [Fact]
    public async Task Without_public_origin_only_configured_origins_are_allowed()
    {
        var client = f.CreateClient();
        var res = await client.SendAsync(Preflight(Public));
        Assert.False(res.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.NotEqual(Public, f.Services.GetRequiredService<IOptions<EmailOptions>>().Value.AppBaseUrl);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Preflight("http://localhost:4200"))).StatusCode);
    }
}
