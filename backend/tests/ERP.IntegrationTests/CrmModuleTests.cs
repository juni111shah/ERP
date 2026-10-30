using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The CRM module (leads, opportunities, interactions): scope checks, lead conversion and pipeline stages.</summary>
public class CrmModuleTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Crm = "/api/v1/m/crm";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<(HttpClient C, string Entity)> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/crm/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Crm " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        return (c, entity);
    }

    private static object NewLead(string entity, string name = "Ada Buyer", string? company = null)
        => new { legalEntityId = entity, fullName = name, companyName = company, email = "ada@example.com", source = 1, estimatedValue = 1000m };

    private static object NewOpp(string entity, object? extra = null, int stage = 1)
        => new { legalEntityId = entity, title = "Deal", customerName = "Acme", stage, probabilityPercent = 20, expectedRevenue = 5000m };

    [Fact]
    public async Task Crm_records_need_a_real_legal_entity_customer_and_related_record()
    {
        var (c, entity) = await SetupAsync();
        var ghost = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/leads", NewLead(ghost.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/opportunities", NewOpp(ghost.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/opportunities", new { legalEntityId = entity, title = "Deal", customerPartyId = ghost, customerName = "Acme", stage = 1, probabilityPercent = 20, expectedRevenue = 1m })).StatusCode);

        var lead = await Ok(await c.PostAsJsonAsync($"{Crm}/leads", NewLead(entity)), HttpStatusCode.Created);
        object Interaction(string kind, string id) => new { legalEntityId = entity, relatedEntityKind = kind, relatedEntityId = id, type = 1, subject = "Call", isResolved = true, performedByUserId = Guid.NewGuid(), performedByName = "Someone Else" };

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/interactions", Interaction("Lead", ghost.ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/interactions", Interaction("Spaceship", (string)lead["id"]!))).StatusCode);

        var logged = await Ok(await c.PostAsJsonAsync($"{Crm}/interactions", Interaction("lead", (string)lead["id"]!)), HttpStatusCode.Created);
        Assert.Equal("Lead", (string)logged["relatedEntityKind"]!);
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        Assert.NotEqual("Someone Else", (string?)logged["performedByName"]); // the caller is recorded, not what the request claims
        Assert.Equal((string)me["user"]!["id"]!, (string)logged["performedByUserId"]!);
    }

    private static object LeadUpdate(string id, string entity, int status)
        => new { id, fullName = "Ada Buyer", email = "ada@example.com", source = 1, status, estimatedValue = 1000m };

    [Fact]
    public async Task A_lead_converts_once_and_a_converted_lead_is_frozen()
    {
        var (c, entity) = await SetupAsync();
        var lead = await Ok(await c.PostAsJsonAsync($"{Crm}/leads", NewLead(entity, company: "Acme Trading")), HttpStatusCode.Created);
        var id = (string)lead["id"]!;

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"{Crm}/leads/{id}", LeadUpdate(id, entity, 5))).StatusCode); // Converted is set only by converting
        await Ok(await c.PutAsJsonAsync($"{Crm}/leads/{id}", LeadUpdate(id, entity, 3)));

        var oppId = (string)(await Ok(await c.PostAsJsonAsync($"{Crm}/leads/{id}/convert", new { id, opportunityTitle = "First deal", expectedRevenue = 100m }))).AsValue().GetValue<string>();
        var converted = await Ok(await c.GetAsync($"{Crm}/leads/{id}"));
        Assert.Equal(("converted", oppId), (((string)converted["status"]!).ToLowerInvariant(), (string)converted["convertedOpportunityId"]!));
        var partyId = (string)converted["convertedPartyId"]!;

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Crm}/leads/{id}/convert", new { id, opportunityTitle = "Second deal", expectedRevenue = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Crm}/leads/{id}", LeadUpdate(id, entity, 2))).StatusCode);
        var after = await Ok(await c.GetAsync($"{Crm}/leads/{id}"));
        Assert.Equal((oppId, partyId), ((string)after["convertedOpportunityId"]!, (string)after["convertedPartyId"]!));
    }

    [Fact]
    public async Task An_unqualified_lead_cannot_be_converted_until_it_is_requalified()
    {
        var (c, entity) = await SetupAsync();
        var lead = await Ok(await c.PostAsJsonAsync($"{Crm}/leads", NewLead(entity)), HttpStatusCode.Created);
        var id = (string)lead["id"]!;
        await Ok(await c.PutAsJsonAsync($"{Crm}/leads/{id}", LeadUpdate(id, entity, 4)));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Crm}/leads/{id}/convert", new { id, opportunityTitle = "Deal", expectedRevenue = 1m })).StatusCode);
        await Ok(await c.PutAsJsonAsync($"{Crm}/leads/{id}", LeadUpdate(id, entity, 3)));
        await Ok(await c.PostAsJsonAsync($"{Crm}/leads/{id}/convert", new { id, opportunityTitle = "Deal", expectedRevenue = 1m }));
    }

    [Fact]
    public async Task Opportunity_close_follows_the_pipeline_and_reopening_clears_the_close()
    {
        var (c, entity) = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Crm}/opportunities", NewOpp(entity, stage: 4))).StatusCode); // cannot be born closed
        var opp = await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities", NewOpp(entity)), HttpStatusCode.Created);
        var id = (string)opp["id"]!;

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Crm}/opportunities/{id}/close", new { id, isWon = true, actualRevenue = 5000m })).StatusCode); // not yet in negotiation
        await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{id}/stage", new { id, stage = 2, probabilityPercent = 40 }));
        await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{id}/stage", new { id, stage = 3, probabilityPercent = 60 }));
        var won = await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{id}/close", new { id, isWon = true, actualRevenue = 4800m, closeReason = "Price" }));
        Assert.Equal((100, 4800m), ((int)won["probabilityPercent"]!, (decimal)won["actualRevenue"]!));

        var lost = await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities", NewOpp(entity)), HttpStatusCode.Created);
        var lostId = (string)lost["id"]!;
        var closed = await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{lostId}/stage", new { id = lostId, stage = 5, probabilityPercent = 55 }));
        Assert.Equal(0, (int)closed["probabilityPercent"]!); // closing lost forces 0%, whatever the request says
        await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{lostId}/close", new { id = lostId, isWon = false, actualRevenue = 0m }), HttpStatusCode.Conflict);
        var reopened = await Ok(await c.PostAsJsonAsync($"{Crm}/opportunities/{lostId}/stage", new { id = lostId, stage = 1, probabilityPercent = 20 }));
        Assert.Null((string?)reopened["actualCloseDate"]);
        Assert.Equal(0m, (decimal)reopened["actualRevenue"]!);
    }
}
