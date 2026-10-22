using System.Net;
using System.Net.Http.Json;

namespace ERP.IntegrationTests;

public class EnvelopeAndListTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Success_responses_use_the_standard_envelope()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var env = await ApiFactory.Envelope(await c.GetAsync("/api/v1/me"));
        Assert.True((bool)env["success"]!);
        Assert.Equal(200, (int)env["statusCode"]!);
        Assert.NotNull(env["data"]!["user"]);
        Assert.False(string.IsNullOrEmpty((string?)env["traceId"]));
    }

    [Fact]
    public async Task Errors_use_the_envelope_with_rfc7807_members()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var res = await c.PostAsJsonAsync("/api/v1/parties", new { name = "", kind = "Company", roles = "Customer" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var env = await ApiFactory.Envelope(res);
        Assert.False((bool)env["success"]!);
        Assert.Equal(400, (int)env["statusCode"]!);
        Assert.Equal(400, (int)env["status"]!);
        Assert.NotNull(env["title"]);
        Assert.NotNull(env["errors"]);
        Assert.NotNull(env["traceId"]);

        var anon = await ApiFactory.Envelope(await f.Client().GetAsync("/api/v1/me"));
        Assert.False((bool)anon["success"]!);
        Assert.Equal(401, (int)anon["statusCode"]!);
    }

    [Fact]
    public async Task Generic_list_pages_sorts_searches_and_filters()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        foreach (var n in new[] { "Zeta Paging", "Alpha Paging", "Mid Paging" })
            Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/parties", new { name = n, kind = "Company", roles = "Vendor" })).StatusCode);

        var env = await ApiFactory.Envelope(await c.GetAsync("/api/v1/parties?search=Paging&sort=-name&pageSize=2&page=1"));
        var items = env["data"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.Equal("Zeta Paging", (string?)items[0]!["name"]);
        Assert.Equal(3, (int)env["meta"]!["total"]!);
        Assert.Equal(2, (int)env["meta"]!["totalPages"]!);
        Assert.True((bool)env["meta"]!["hasNext"]!);

        var vendors = (await ApiFactory.Json(await c.GetAsync("/api/v1/parties?search=Paging&filter=roles:has:vendor"))).AsArray();
        Assert.Equal(3, vendors.Count);
        var raw = await c.GetAsync("/api/v1/parties?search=Paging&role=customer");
        Assert.True(raw.IsSuccessStatusCode, await raw.Content.ReadAsStringAsync());
        var customers = (await ApiFactory.Json(raw)).AsArray();
        Assert.Empty(customers);
    }

    [Fact]
    public async Task Sorting_or_filtering_on_a_non_whitelisted_field_is_a_validation_error()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/parties?sort=iban")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/parties?filter=idNumber:eq:1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/v1/parties?filter=isActive:gt:true")).StatusCode);
    }
}
