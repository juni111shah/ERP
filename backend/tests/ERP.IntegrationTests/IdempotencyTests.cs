using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Domain.Catalog;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>TASK-010: Idempotency-Key replays and all-or-nothing document postings.</summary>
public class IdempotencyTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Json(HttpResponseMessage res) => ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    private static HttpRequestMessage Post(string url, object body, string? key)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (key is not null) req.Headers.Add("Idempotency-Key", key);
        return req;
    }

    private async Task<int> CountPartiesAsync(HttpClient c, string name)
        => (await ApiFactory.Json(await c.GetAsync($"/api/v1/parties?search={name}"))).AsArray().Count;

    [Fact]
    public async Task The_same_key_twice_returns_the_first_response_and_creates_one_document()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var name = "Idem" + Guid.NewGuid().ToString("N")[..8];
        var body = new { name, kind = "Company", roles = "Customer" };
        var key = Guid.NewGuid().ToString();

        var first = await c.SendAsync(Post("/api/v1/parties", body, key));
        var second = await c.SendAsync(Post("/api/v1/parties", body, key));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.True(second.Headers.Contains("Idempotent-Replay"));
        Assert.Equal((string)(await Json(first))["id"]!, (string)(await Json(second))["id"]!);
        Assert.Equal(1, await CountPartiesAsync(c, name));
    }

    [Fact]
    public async Task Different_keys_create_separate_documents()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var name = "Idem" + Guid.NewGuid().ToString("N")[..8];
        var body = new { name, kind = "Company", roles = "Customer" };

        var first = await c.SendAsync(Post("/api/v1/parties", body, Guid.NewGuid().ToString()));
        var second = await c.SendAsync(Post("/api/v1/parties", new { name = name + "b", kind = "Company", roles = "Customer" }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.NotEqual((string)(await Json(first))["id"]!, (string)(await Json(second))["id"]!);
    }

    [Fact]
    public async Task A_reused_key_with_a_different_body_is_rejected()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var key = Guid.NewGuid().ToString();
        var a = "Idem" + Guid.NewGuid().ToString("N")[..8];
        Assert.Equal(HttpStatusCode.Created, (await c.SendAsync(Post("/api/v1/parties", new { name = a, kind = "Company", roles = "Customer" }, key))).StatusCode);
        var clash = await c.SendAsync(Post("/api/v1/parties", new { name = a + "x", kind = "Company", roles = "Customer" }, key));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, clash.StatusCode);
        Assert.Equal(0, await CountPartiesAsync(c, a + "x"));
    }

    [Fact]
    public async Task A_failed_request_releases_the_key_so_it_can_be_retried()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var key = Guid.NewGuid().ToString();
        var bad = await c.SendAsync(Post("/api/v1/parties", new { name = "", kind = "Company", roles = "Customer" }, key));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var name = "Idem" + Guid.NewGuid().ToString("N")[..8];
        var again = await c.SendAsync(Post("/api/v1/parties", new { name = "", kind = "Company", roles = "Customer" }, key));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.False(again.Headers.Contains("Idempotent-Replay"));
    }

    [Fact]
    public async Task A_posting_failure_leaves_no_pos_order_and_no_session_totals()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;
        var reg = await c.PostAsJsonAsync("/api/v1/m/pos/registers", new { legalEntityId = entity, code = "RG-" + Guid.NewGuid().ToString("N")[..4], name = "Tx Register" });
        var regId = (string)(await Json(reg))["id"]!;
        var open = await c.PostAsJsonAsync("/api/v1/m/pos/sessions/open", new { legalEntityId = entity, registerId = regId, cashierUserId = Guid.NewGuid(), cashierName = "C", openingCash = 0m });
        var sessionId = (string)(await Json(open))["id"]!;
        var item = await c.PostAsJsonAsync("/api/v1/items", new ItemInput("TX-" + Guid.NewGuid().ToString("N")[..6], "Tx item", ItemType.Product, "ea", 10m, 4m, null));
        var itemId = Guid.Parse((string)(await Json(item))["id"]!);

        // Lock the books so the ledger posting fails after the order row was written; the whole sale must roll back.
        var lockRes = await c.PutAsJsonAsync($"/api/v1/legal-entities/{entity}/books-lock", new { through = DateTime.UtcNow.ToString("yyyy-MM-dd") });
        lockRes.EnsureSuccessStatusCode();
        var res = await c.PostAsJsonAsync("/api/v1/m/pos/orders", new CreatePosOrderCommand(Guid.Parse(entity), Guid.Parse(regId), Guid.Parse(sessionId), null, "W",
            [new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 10m, DiscountPercent: 0)], [new PosPaymentInput(PosPaymentMethod.Cash, 10m, 10m, 0m, null)]));
        Assert.False(res.IsSuccessStatusCode, "the sale must fail when the ledger cannot be posted");

        var orders = (await ApiFactory.Json(await c.GetAsync($"/api/v1/m/pos/orders?sessionId={sessionId}"))).AsArray();
        Assert.Empty(orders.Where(o => (string?)o!["sessionId"] == sessionId));
        var session = await Json(await c.GetAsync($"/api/v1/m/pos/sessions/current?registerId={regId}"));
        Assert.Equal(0m, (decimal)session["totalSales"]!);
        Assert.Equal(0m, (decimal)session["expectedCash"]!);
    }
}
