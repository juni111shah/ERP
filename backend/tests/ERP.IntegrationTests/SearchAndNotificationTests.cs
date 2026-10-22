using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Global search across core and module records, and the notification centre.</summary>
public class SearchAndNotificationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Ok(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Search_finds_core_records_and_needs_two_characters()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var name = "Zebracorp" + Guid.NewGuid().ToString("N")[..4];
        await c.PostAsJsonAsync("/api/v1/parties", new { name, kind = "Company", roles = "Customer" });
        var result = await Ok(await c.GetAsync($"/api/v1/search?q={name}"));
        var hit = result["hits"]!.AsArray().Single(h => (string?)h!["category"] == "Parties")!;
        Assert.Equal(name, (string)hit["title"]!);
        Assert.Empty((await Ok(await c.GetAsync("/api/v1/search?q=z")))["hits"]!.AsArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/search?q=abc")).StatusCode);
    }

    [Fact]
    public async Task Search_includes_module_records_only_when_the_module_is_enabled_and_the_caller_may_read_them()
    {
        var (owner, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await owner.GetAsync("/api/v1/me"));
        var unique = "Searchable" + Guid.NewGuid().ToString("N")[..4];
        (await owner.PostAsJsonAsync("/api/v1/m/realestate/properties", new { legalEntityId = (string)me["legalEntities"]![0]!["id"]!, name = unique, propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1_000_000m })).EnsureSuccessStatusCode();
        var hits = (await Ok(await owner.GetAsync($"/api/v1/search?q={unique}")))["hits"]!.AsArray();
        Assert.Contains(hits, h => (string?)h!["category"] == "Properties" && ((string)h["link"]!).StartsWith("/real-estate/properties/"));

        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail); // general workspace: no real estate module
        Assert.DoesNotContain((await Ok(await admin.GetAsync($"/api/v1/search?q={unique}")))["hits"]!.AsArray(), h => (string?)h!["category"] == "Properties");
    }

    [Fact]
    public async Task Notifications_can_all_be_marked_read_at_once()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        // an approval request creates work for approvers; make the admin's own list non-empty by being notified of a decision
        var request = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/approvals", new { subject = "Notify me", amount = 10 }));
        var roles = await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"));
        var role = (string)roles.AsArray().First(r => (string?)r!["name"] == "Approver")!["id"]!;
        var email = $"nt{Guid.NewGuid():N}"[..10] + "@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "N", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { role } })).EnsureSuccessStatusCode();
        var approver = (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
        (await approver.PostAsJsonAsync($"/api/v1/approvals/{(string)request["id"]!}/approve", new { comment = "ok" })).EnsureSuccessStatusCode();

        Assert.True((int)(await Ok(await admin.GetAsync("/api/v1/notifications")))["unread"]! >= 1);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync("/api/v1/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (int)(await Ok(await admin.GetAsync("/api/v1/notifications")))["unread"]!);
    }
}
