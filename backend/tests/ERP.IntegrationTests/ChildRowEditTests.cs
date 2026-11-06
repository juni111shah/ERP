using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>
/// Edits that replace a record's lines through its collection. The new rows have client-side ids, so they must be inserted rather than
/// treated as existing rows, and saved with the workspace of the record.
/// </summary>
public class ChildRowEditTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Dist = "/api/v1/m/distribution";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Editing_a_route_saves_its_fields_and_replaces_its_stops()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/distribution/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Stops " + tag, countryCode = "AE", baseCurrency = "AED" }), HttpStatusCode.Created))["id"]!;
        var route = (string)(await Ok(await c.PostAsJsonAsync($"{Dist}/routes", new { legalEntityId = entity, code = "R" + tag, name = "North" }), HttpStatusCode.Created))["id"]!;

        object Stop(string name, int order) => new { customerPartyId = Guid.NewGuid(), customerName = name, sequenceOrder = order };

        await Ok(await c.PutAsJsonAsync($"{Dist}/routes/{route}", new { id = route, name = "North 2", vehicleNumber = "V1", isActive = true, stops = new[] { Stop("First", 1), Stop("Second", 2) } }));
        var saved = await Ok(await c.GetAsync($"{Dist}/routes/{route}"));
        Assert.Equal("North 2", (string)saved["name"]!);
        Assert.Equal("V1", (string)saved["vehicleNumber"]!);
        Assert.Equal(2, saved["stops"]!.AsArray().Count);

        await Ok(await c.PutAsJsonAsync($"{Dist}/routes/{route}", new { id = route, name = "North 2", vehicleNumber = "V1", isActive = true, stops = new[] { Stop("Only", 1) } }));
        var replaced = await Ok(await c.GetAsync($"{Dist}/routes/{route}"));
        var stops = replaced["stops"]!.AsArray();
        Assert.Single(stops);
        Assert.Equal("Only", (string)stops[0]!["customerName"]!);
    }
}
