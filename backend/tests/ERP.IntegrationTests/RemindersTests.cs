using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The reminder engine: due items become in-app notifications once, and every message lands in the communication log.</summary>
public class RemindersTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task A_due_bill_reminds_the_payables_team_once_and_is_logged()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Rem " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var expense = (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!;
        var due = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var reference = "REM-" + Guid.NewGuid().ToString("N")[..6];
        var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = entity, partyId = vendor, vendorReference = reference, billDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), dueDate = due, lines = new[] { new { description = "x", quantity = 1m, price = 100m, accountId = expense } } }), HttpStatusCode.Created);
        await Ok(await c.PostAsync($"{Fin}/bills/{(string)draft["id"]!}/post", null));

        var first = (int)(await Ok(await c.PostAsync("/api/v1/messages/send-reminders", null)))["sent"]!;
        Assert.True(first >= 1);
        Assert.Equal(0, (int)(await Ok(await c.PostAsync("/api/v1/messages/send-reminders", null)))["sent"]!); // idempotent

        var items = (await Ok(await c.GetAsync("/api/v1/messages?pageSize=100"))).AsArray();
        Assert.Contains(items, m => (string?)m!["channel"] == "inapp" && ((string)m["subject"]!).Contains("is due"));
        Assert.Contains((await Ok(await c.GetAsync("/api/v1/notifications")))["items"]!.AsArray(), n => ((string)n!["title"]!).Contains("is due"));
    }

    [Fact]
    public async Task Sending_reminders_needs_settings_permission()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsync("/api/v1/messages/send-reminders", null)).StatusCode);
}
