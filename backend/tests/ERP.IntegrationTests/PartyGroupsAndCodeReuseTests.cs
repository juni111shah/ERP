using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Party groups (CRUD, on parties, in the receivables ageing) and reuse of a deleted account's code.</summary>
public class PartyGroupsAndCodeReuseTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    [Fact]
    public async Task A_deleted_accounts_code_can_be_used_again_but_a_live_duplicate_is_still_refused()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var code = U("R");
        var created = await Ok(await c.PostAsJsonAsync($"{Fin}/accounts", new { code, name = "First", type = "expense", isPostable = true }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Fin}/accounts", new { code, name = "Duplicate", type = "expense", isPostable = true })).StatusCode);

        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.Accounts.IgnoreQueryFilters().SingleAsync(a => a.Id == Guid.Parse((string)created["id"]!));
            account.IsDeleted = true; // soft delete, as the repositories do
            await db.SaveChangesAsync();
        }

        var again = await Ok(await c.PostAsJsonAsync($"{Fin}/accounts", new { code, name = "Second life", type = "expense", isPostable = true }), HttpStatusCode.Created);
        Assert.Equal(code, (string)again["code"]!);
        Assert.NotEqual((string)created["id"]!, (string)again["id"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Fin}/accounts", new { code, name = "Third", type = "expense", isPostable = true })).StatusCode);
    }

    [Fact]
    public async Task Party_groups_are_managed_assigned_to_parties_and_protected_while_in_use()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var code = U("g");
        var group = await Ok(await c.PostAsJsonAsync("/api/v1/party-groups", new { code, name = "Key accounts" }), HttpStatusCode.Created);
        var id = (string)group["id"]!;
        Assert.Equal(code.ToUpperInvariant(), (string)group["code"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/v1/party-groups", new { code, name = "Again" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/party-groups", new { code = "", name = "" })).StatusCode);
        Assert.Equal("Wholesale", (string)(await Ok(await c.PutAsJsonAsync($"/api/v1/party-groups/{id}", new { code, name = "Wholesale", isActive = true })))["name"]!);
        Assert.Contains((await Ok(await c.GetAsync($"/api/v1/party-groups?search={code.ToUpperInvariant()}"))).AsArray(), g => (string)g!["id"]! == id);

        var party = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = U("Grouped "), kind = "Company", roles = "Customer", partyGroupId = id }), HttpStatusCode.Created);
        Assert.Equal(id, (string)party["partyGroupId"]!);
        var listed = (await Ok(await c.GetAsync($"/api/v1/parties?filter=partyGroupId:eq:{id}"))).AsArray();
        Assert.Equal((string)party["id"]!, (string)Assert.Single(listed)!["id"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/parties", new { name = U("Bad "), kind = "Company", roles = "Customer", partyGroupId = Guid.NewGuid() })).StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"/api/v1/party-groups/{id}")).StatusCode); // still used
        await Ok(await c.PutAsJsonAsync($"/api/v1/parties/{(string)party["id"]!}", new { name = (string)party["name"]!, kind = "Company", roles = "Customer", partyGroupId = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/party-groups/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/v1/party-groups/{id}", new { code, name = "Gone" })).StatusCode);
    }

    [Fact]
    public async Task Receivables_ageing_can_be_filtered_and_grouped_by_party_group()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = U("Grp "), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vip = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/party-groups", new { code = U("v"), name = "VIP" }), HttpStatusCode.Created))["id"]!;
        var retail = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/party-groups", new { code = U("r"), name = "Retail" }), HttpStatusCode.Created))["id"]!;

        async Task<string> CustomerAsync(string? group)
            => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = U("Cust "), kind = "Company", roles = "Customer", paymentTermsDays = 30, partyGroupId = group }), HttpStatusCode.Created))["id"]!;
        async Task IssueAsync(string party, decimal price)
        {
            var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = party, invoiceDate = "2026-06-01", lines = new[] { new { description = "Work", quantity = 1m, price } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));
        }
        await IssueAsync(await CustomerAsync(vip), 100m);
        await IssueAsync(await CustomerAsync(vip), 50m);
        await IssueAsync(await CustomerAsync(retail), 70m);
        await IssueAsync(await CustomerAsync(null), 30m);

        var q = $"asOf=2026-08-01&legalEntityId={entity}";
        var all = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}"));
        Assert.Equal(250m, (decimal)all["totals"]!["total"]!);

        var onlyVip = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&partyGroupId={vip}"));
        Assert.Equal(150m, (decimal)onlyVip["totals"]!["total"]!);
        Assert.Equal(2, onlyVip["rows"]!.AsArray().Count); // two customers in the group

        var byGroup = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=partyGroup"));
        var rows = byGroup["rows"]!.AsArray();
        Assert.Equal(150m, (decimal)rows.Single(r => (string)r!["partyName"]! == "VIP")!["total"]!);
        Assert.Equal(70m, (decimal)rows.Single(r => (string)r!["partyName"]! == "Retail")!["total"]!);
        Assert.Equal(30m, (decimal)rows.Single(r => (string)r!["partyName"]! == "No group")!["total"]!);

        var vipByGroup = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=partyGroup&partyGroupId={vip}"));
        Assert.Equal("VIP", (string)Assert.Single(vipByGroup["rows"]!.AsArray())!["partyName"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=nonsense")).StatusCode);
    }

    [Fact]
    public async Task Party_group_endpoints_need_a_signed_in_user()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/party-groups")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/party-groups", new { code = "x", name = "x" })).StatusCode);
    }
}
