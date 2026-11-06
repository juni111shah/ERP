using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;

namespace ERP.IntegrationTests;

/// <summary>One schema clash anywhere fails the whole document, so this covers every endpoint in every module.</summary>
public class SwaggerDocumentTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Swagger_json_generates_and_lists_the_endpoints()
    {
        var res = await f.WithWebHostBuilder(b => b.UseSetting("Swagger:Enabled", "true"))
            .CreateClient().GetAsync("/swagger/v1/swagger.json");
        var body = await res.Content.ReadAsStringAsync();

        Assert.True(res.StatusCode == HttpStatusCode.OK, body);
        var paths = JsonNode.Parse(body)!["paths"]!.AsObject();
        Assert.Contains("/api/v1/m/projects/timesheets/submit", paths.Select(p => p.Key));
        Assert.Contains("/api/v1/auth/login", paths.Select(p => p.Key));
    }
}
