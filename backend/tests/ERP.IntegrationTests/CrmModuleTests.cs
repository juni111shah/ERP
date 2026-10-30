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
}
