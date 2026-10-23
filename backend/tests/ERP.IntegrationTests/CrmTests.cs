using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Real estate CRM: listings, leads, pipeline, lead-to-deal conversion, reminders and search.</summary>
public class CrmTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string PropertyId, string UnitId);

    private async Task<Ctx> SetupAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var code = "C" + Guid.NewGuid().ToString("N")[..6];
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code, name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "Crm " + code, propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1m }), HttpStatusCode.Created);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "5", unitTypeId = (string)type["id"]!, areaSqft = 800, askingRent = 90_000m }), HttpStatusCode.Created);
        return new Ctx(c, entity, (string)prop["id"]!, (string)unit["id"]!);
    }

    [Fact]
    public async Task A_listing_is_published_and_closed_in_order()
    {
        var x = await SetupAsync();
        var l = await Ok(await x.C.PostAsJsonAsync($"{Re}/listings", new { propertyId = x.PropertyId, unitId = x.UnitId, kind = "rent", title = "Bright 1BR", price = 90_000m, channels = "Bayut, website" }), HttpStatusCode.Created);
        var id = (string)l["id"]!;
        Assert.Equal("draft", (string)l["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/listings/{id}/status", new { status = "closed" })).StatusCode); // must be published first
        var published = await Ok(await x.C.PostAsJsonAsync($"{Re}/listings/{id}/status", new { status = "published" }));
        Assert.NotNull((string?)published["listedOn"]);
        var closed = await Ok(await x.C.PostAsJsonAsync($"{Re}/listings/{id}/status", new { status = "closed" }));
        Assert.NotNull((string?)closed["closedOn"]);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Re}/listings/{id}", new { propertyId = x.PropertyId, kind = "rent", title = "x", price = 1m })).StatusCode);
        Assert.Contains((await Ok(await x.C.GetAsync($"{Re}/listings?pageSize=100"))).AsArray(), r => (string)r!["id"]! == id);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/listings", new { propertyId = x.PropertyId, unitId = Guid.NewGuid(), kind = "rent", title = "x", price = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/listings", new { propertyId = x.PropertyId, kind = "rent", title = "x", price = 0m })).StatusCode);
    }

    [Fact]
    public async Task A_lead_moves_through_the_pipeline_and_converts_into_a_deal()
    {
        var x = await SetupAsync();
        var listing = await Ok(await x.C.PostAsJsonAsync($"{Re}/listings", new { propertyId = x.PropertyId, unitId = x.UnitId, kind = "sale", title = "Flat for sale", price = 1_500_000m }), HttpStatusCode.Created);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var lead = await Ok(await x.C.PostAsJsonAsync($"{Re}/leads", new { legalEntityId = x.Entity, name = "Lead " + tag, phone = "+971500000000", source = "Website", listingId = (string)listing["id"]!, followUpOn = "2020-01-01" }), HttpStatusCode.Created);
        var id = (string)lead["id"]!;
        Assert.Equal("new", (string)lead["stage"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/leads", new { legalEntityId = x.Entity, name = "No contact" })).StatusCode); // needs phone or email
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "won" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "lost" })).StatusCode); // reason required
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "contacted" }));
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "viewing" }));

        var pipeline = await Ok(await x.C.GetAsync($"{Re}/leads/pipeline"));
        Assert.True((int)pipeline["open"]! >= 1);
        Assert.True((int)pipeline["followUpsDue"]! >= 1);

        var converted = await Ok(await x.C.PostAsJsonAsync($"{Re}/leads/{id}/convert", new { commissionRatePercent = 2 }));
        Assert.Equal("won", (string)converted["stage"]!);
        var deal = await Ok(await x.C.GetAsync($"{Re}/deals/{(string)converted["dealId"]!}"));
        Assert.Equal("sale", (string)deal["dealType"]!);
        Assert.Equal(1_500_000m, (decimal)deal["dealValue"]!);
        Assert.Equal(30_000m, (decimal)deal["grossCommission"]!);
        Assert.Equal(x.PropertyId, (string)deal["propertyId"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leads/{id}/convert", new { })).StatusCode); // only once
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "contacted" })).StatusCode);
    }

    [Fact]
    public async Task Lost_leads_can_be_reopened_and_the_reminder_and_search_find_leads()
    {
        var x = await SetupAsync();
        var tag = "Zq" + Guid.NewGuid().ToString("N")[..6];
        var lead = await Ok(await x.C.PostAsJsonAsync($"{Re}/leads", new { legalEntityId = x.Entity, name = tag, email = "lead@example.com", followUpOn = "2020-01-01" }), HttpStatusCode.Created);
        var id = (string)lead["id"]!;

        var hits = (await Ok(await x.C.GetAsync($"/api/v1/search?q={tag}")))["hits"]!.AsArray();
        Assert.Contains(hits, h => (string?)h!["category"] == "Leads");

        var sent = (int)(await Ok(await x.C.PostAsync("/api/v1/messages/send-reminders", null)))["sent"]!;
        Assert.True(sent >= 1);
        var messages = (await Ok(await x.C.GetAsync("/api/v1/messages?pageSize=100"))).AsArray();
        Assert.Contains(messages, m => ((string)m!["subject"]!).Contains(tag));

        var lost = await Ok(await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "lost", lostReason = "Bought elsewhere" }));
        Assert.Equal("Bought elsewhere", (string)lost["lostReason"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "viewing" })).StatusCode);
        var reopened = await Ok(await x.C.PostAsJsonAsync($"{Re}/leads/{id}/stage", new { stage = "new" }));
        Assert.Null((string?)reopened["lostReason"]);
    }
}
