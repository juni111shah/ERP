using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Hierarchical chart-of-accounts numbering (mask setting, next-code suggestion, validation) and the roll-up rule for children of another type.</summary>
public class AccountNumberingIntegrationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static Task<HttpResponseMessage> SetMask(HttpClient c, string mode, int[]? segments = null, string sep = "-")
        => c.PutAsJsonAsync($"{Fin}/coa-numbering", new { mode, segments = segments ?? [4, 2, 2], separator = sep });

    private static async Task<string> NextAsync(HttpClient c, string? parentId, string? type = null)
    {
        var query = string.Join("&", new[] { parentId is null ? null : $"parentId={parentId}", type is null ? null : $"type={type}" }.Where(x => x is not null));
        return (string)(await Ok(await c.GetAsync($"{Fin}/accounts/next-code?{query}")))["code"]!;
    }

    private static Task<HttpResponseMessage> Create(HttpClient c, string code, string name, string type, string? parentId = null)
        => c.PostAsJsonAsync($"{Fin}/accounts", new { code, name, type, parentId, isPostable = true });

    [Fact]
    public async Task Free_mode_is_the_default_suggests_nothing_and_accepts_any_code()
    {
        var c = await AdminAsync();
        var mask = await Ok(await c.GetAsync($"{Fin}/coa-numbering"));
        Assert.Equal("free", (string)mask["mode"]!);
        Assert.Equal("", await NextAsync(c, null, "asset"));
        await Ok(await Create(c, "X-" + Guid.NewGuid().ToString("N")[..6], "Free code", "asset"), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Hierarchical_mode_suggests_validates_and_protects_accounts_with_postings()
    {
        var c = await AdminAsync();
        await Ok(await SetMask(c, "hierarchical"));
        try
        {
            var all = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
            var seeded = all.Select(a => (string)a!["code"]!).ToHashSet();
            var root = await NextAsync(c, null, "expense");
            Assert.Matches("^\\d{4}$", root);
            Assert.DoesNotContain(root, seeded);
            var rootId = (string)(await Ok(await Create(c, root, "Numbered root", "expense"), HttpStatusCode.Created))["id"]!;

            Assert.Equal($"{root}-01", await NextAsync(c, rootId));
            var child = (string)(await Ok(await Create(c, $"{root}-01", "Child one", "expense", rootId), HttpStatusCode.Created))["id"]!;
            Assert.Equal($"{root}-02", await NextAsync(c, rootId));
            Assert.Equal($"{root}-01-01", await NextAsync(c, child));
            var grand = (string)(await Ok(await Create(c, $"{root}-01-01", "Grandchild", "expense", child), HttpStatusCode.Created))["id"]!;
            Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Fin}/accounts/next-code?parentId={grand}")).StatusCode); // already at the deepest level

            // codes must follow the mask, the parent and the parent's type
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(c, $"{root}-99", "Wrong parent prefix", "expense", child)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(c, $"{root}-5", "Short segment", "expense", rootId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(c, "ABCD", "Not digits", "expense")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(c, $"{root}-03", "Wrong type", "asset", rootId)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await Create(c, $"{root}-03", "Child without parent", "expense")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Create(c, $"{root}-01", "Duplicate", "expense", rootId)).StatusCode);

            // an account that already has postings cannot get its first child
            var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Num " + root, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
            var bank = (string)all.First(a => (string?)a!["code"] == "1010")!["id"]!;
            var usedCode = await NextAsync(c, null, "expense");
            var used = (string)(await Ok(await Create(c, usedCode, "Used expense", "expense"), HttpStatusCode.Created))["id"]!;
            var j = await Ok(await c.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = entity, date = "2026-05-01", lines = new[] { new { accountId = used, debit = 10m, credit = 0m }, new { accountId = bank, debit = 0m, credit = 10m } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/journals/{(string)j["id"]!}/post", null));
            Assert.Equal(HttpStatusCode.Conflict, (await Create(c, $"{usedCode}-01", "First child of a used account", "expense", used)).StatusCode);

            // system accounts keep their code
            var system = all.First(a => (string?)a!["code"] == "1010")!;
            var rename = await c.PutAsJsonAsync($"{Fin}/accounts/{(string)system["id"]!}", new { code = "1019", name = (string)system["name"]!, type = "asset", isPostable = true, isBank = true, isActive = true });
            Assert.Equal(HttpStatusCode.Conflict, rename.StatusCode);

            // editing an account without touching its code, parent or type is never blocked by the mask
            var legacy = all.First(a => (string?)a!["code"] == "6900")!;
            await Ok(await c.PutAsJsonAsync($"{Fin}/accounts/{(string)legacy["id"]!}", new { code = "6900", name = "General expenses (edited)", type = "expense", isPostable = true, isActive = true }));
        }
        finally { await SetMask(c, "free"); }
    }

    [Fact]
    public async Task The_mask_is_validated_and_needs_sign_in()
    {
        var c = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await SetMask(c, "hierarchical", [6, 6, 6, 6])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetMask(c, "hierarchical", [4, 2], "#")).StatusCode);
        var ok = await Ok(await SetMask(c, "hierarchical", [3, 2], "."));
        try
        {
            Assert.Equal("000.00", (string)ok["pattern"]!);
            var first = await NextAsync(c, null, "asset");
            Assert.Matches("^\\d{3}$", first);
        }
        finally { await SetMask(c, "free"); }
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Fin}/coa-numbering")).StatusCode);
    }

    [Fact]
    public async Task A_child_of_another_account_type_does_not_roll_up_into_its_parent()
    {
        var c = await AdminAsync();
        var t = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "RollUp " + t, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var parent = (string)(await Ok(await Create(c, $"R{t}0", "Roll parent", "asset"), HttpStatusCode.Created))["id"]!;
        var sameType = (string)(await Ok(await Create(c, $"R{t}1", "Roll same type", "asset", parent), HttpStatusCode.Created))["id"]!;
        var otherType = (string)(await Ok(await Create(c, $"R{t}2", "Roll other type", "income", parent), HttpStatusCode.Created))["id"]!;
        var j = await Ok(await c.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = entity, date = "2026-05-01", lines = new[] { new { accountId = sameType, debit = 70m, credit = 0m }, new { accountId = otherType, debit = 0m, credit = 70m } } }), HttpStatusCode.Created);
        await Ok(await c.PostAsync($"{Fin}/journals/{(string)j["id"]!}/post", null));

        var list = await Ok(await c.GetAsync($"{Fin}/accounts/tree/categories/asset/accounts?search=R{t}&includeBalances=true&legalEntityId={entity}"));
        Assert.Equal(70m, (decimal)list.AsArray().Single(x => (string)x!["id"]! == parent)!["balance"]!); // the income child is not netted into the asset parent
        var children = await Ok(await c.GetAsync($"{Fin}/accounts/{parent}/children?includeBalances=true&legalEntityId={entity}"));
        Assert.Equal(70m, (decimal)children.AsArray().Single(x => (string)x!["id"]! == otherType)!["balance"]!);
    }
}
