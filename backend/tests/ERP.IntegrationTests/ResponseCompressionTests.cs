using System.Net.Http.Headers;

namespace ERP.IntegrationTests;

/// <summary>JSON responses are compressed for clients that accept it and left alone otherwise (TASK-125).</summary>
public class ResponseCompressionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Json_is_compressed_when_the_client_accepts_it()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var gz = new HttpRequestMessage(HttpMethod.Get, "/api/v1/roles");
        gz.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var res = await c.SendAsync(gz);
        res.EnsureSuccessStatusCode();
        Assert.Contains("gzip", res.Content.Headers.ContentEncoding);

        var plain = await c.GetAsync("/api/v1/roles");
        plain.EnsureSuccessStatusCode();
        Assert.Empty(plain.Content.Headers.ContentEncoding);
    }
}
