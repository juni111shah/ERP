using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Step-by-step chart of accounts: categories, primary accounts of one category, children of one account, path; scoping and permissions.</summary>
public class AccountTreeTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Base = "/api/v1/finance/accounts";

    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static async Task<string> CreateAsync(HttpClient c, string code, string name, string type, string? parentId = null, bool isPostable = true)
        => (string)(await Ok(await c.PostAsJsonAsync(Base, new { code, name, type, parentId, isPostable }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> NewEntityAsync(HttpClient c)
        => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Tree " + Tag(), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    private async Task<HttpClient> UserWithAsync(HttpClient admin, params string[] permissions)
    {
        var role = await Ok(await admin.PostAsJsonAsync("/api/v1/roles", new { name = "Tree role " + Tag(), description = "test", permissions }), HttpStatusCode.Created);
        var email = $"tree{Tag().ToLowerInvariant()}@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Tree user", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)role["id"]! } })).EnsureSuccessStatusCode();
        return (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
    }

    [Fact]
    public async Task Categories_list_every_account_type_with_counts_and_a_children_flag()
    {
        var c = await AdminAsync();
        var cats = (await Ok(await c.GetAsync($"{Base}/tree/categories"))).AsArray();
        Assert.Equal(new[] { "asset", "liability", "equity", "income", "expense" }, cats.Select(x => (string)x!["type"]!).ToArray());
        var flat = (await Ok(await c.GetAsync($"{Base}?pageSize=200"))).AsArray();
        foreach (var cat in cats)
        {
            var type = (string)cat!["type"]!;
            Assert.Equal(flat.Count(a => (string)a!["type"]! == type), (int)cat["accountCount"]!);
            Assert.Equal((int)cat["primaryCount"]! > 0, (bool)cat["hasChildren"]!);
            Assert.Null(cat["balance"]); // balances are opt-in
        }
    }

    [Fact]
    public async Task One_level_per_request_primary_accounts_then_children_then_path()
    {
        var c = await AdminAsync();
        var t = Tag();
        var root = await CreateAsync(c, $"T{t}0", $"Tree root {t}", "asset", isPostable: false);
        var mid = await CreateAsync(c, $"T{t}1", $"Tree mid {t}", "asset", root, isPostable: false);
        var leaf = await CreateAsync(c, $"T{t}2", $"Tree leaf {t}", "asset", mid);
        var crossType = await CreateAsync(c, $"T{t}3", $"Tree liability under asset {t}", "liability", root);

        // Panel 2: the root is primary, its same-type descendants are not; a child of another type is primary in its own category.
        var assets = await Ok(await c.GetAsync($"{Base}/tree/categories/Asset/accounts?search={t}&pageSize=50"));
        var only = Assert.Single(assets.AsArray());
        Assert.Equal(root, (string)only!["id"]!);
        Assert.True((bool)only["hasChildren"]!);
        Assert.Equal(2, (int)only["childCount"]!);
        var liabilities = await Ok(await c.GetAsync($"{Base}/tree/categories/liability/accounts?search={t}"));
        Assert.Equal(crossType, (string)Assert.Single(liabilities.AsArray())!["id"]!);

        // Panel 3: only direct children, paged with the standard envelope meta.
        var res = await c.GetAsync($"{Base}/{root}/children?pageSize=1");
        var envelope = await ApiFactory.Envelope(res);
        Assert.Equal(2, (int)envelope["meta"]!["total"]!);
        var kids = await Ok(await c.GetAsync($"{Base}/{root}/children"));
        Assert.Equal(new[] { mid, crossType }, kids.AsArray().Select(k => (string)k!["id"]!).ToArray()); // sorted by code
        Assert.True((bool)kids[0]!["hasChildren"]!);
        var deeper = await Ok(await c.GetAsync($"{Base}/{mid}/children"));
        var leafNode = Assert.Single(deeper.AsArray())!;
        Assert.Equal(leaf, (string)leafNode["id"]!);
        Assert.False((bool)leafNode["hasChildren"]!);
        Assert.Empty((await Ok(await c.GetAsync($"{Base}/{leaf}/children"))).AsArray());

        // Single account (for the edit drawer), with custom fields.
        var one = await Ok(await c.GetAsync($"{Base}/{mid}"));
        Assert.Equal(root, (string)one["parentId"]!);
        Assert.NotNull(one["customFields"]);

        // Path for breadcrumbs: root first.
        var path = await Ok(await c.GetAsync($"{Base}/{leaf}/path"));
        Assert.Equal(new[] { root, mid, leaf }, path.AsArray().Select(p => (string)p!["id"]!).ToArray());

        // Filters still apply per level.
        var inactive = await Ok(await c.PutAsJsonAsync($"{Base}/{crossType}", new { code = $"T{t}3", name = "x", type = "liability", parentId = root, isActive = false }));
        Assert.False((bool)inactive["isActive"]!);
        Assert.Equal(new[] { mid }, (await Ok(await c.GetAsync($"{Base}/{root}/children?filter=isActive:eq:true"))).AsArray().Select(k => (string)k!["id"]!).ToArray());
    }

    [Fact]
    public async Task Unknown_type_is_a_validation_error_and_unknown_account_is_not_found()
    {
        var c = await AdminAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Base}/tree/categories/nothing/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Base}/tree/categories/7/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Base}/{Guid.NewGuid()}/children")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Base}/{Guid.NewGuid()}/path")).StatusCode);
    }

    [Fact]
    public async Task Another_workspace_cannot_see_or_drill_into_the_accounts()
    {
        var c = await AdminAsync();
        var t = Tag();
        var root = await CreateAsync(c, $"W{t}0", $"Workspace root {t}", "expense", isPostable: false);
        await CreateAsync(c, $"W{t}1", $"Workspace child {t}", "expense", root);

        var (other, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Base}/{root}/children")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Base}/{root}/path")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Base}/{root}")).StatusCode);
        Assert.Empty((await Ok(await other.GetAsync($"{Base}/tree/categories/expense/accounts?search={t}"))).AsArray());
        var mine = (await Ok(await c.GetAsync($"{Base}/tree/categories"))).AsArray().Sum(x => (int)x!["accountCount"]!);
        var theirs = (await Ok(await other.GetAsync($"{Base}/tree/categories"))).AsArray().Sum(x => (int)x!["accountCount"]!);
        var theirFlat = (await Ok(await other.GetAsync($"{Base}?pageSize=200"))).AsArray().Count;
        Assert.Equal(theirFlat, theirs);
        Assert.NotEqual(mine, theirs);
    }

    [Fact]
    public async Task Balances_are_opt_in_rolled_up_scoped_to_the_legal_entity_and_need_report_access()
    {
        var c = await AdminAsync();
        var t = Tag();
        var (a, b) = (await NewEntityAsync(c), await NewEntityAsync(c));
        var root = await CreateAsync(c, $"B{t}0", $"Bal root {t}", "asset", isPostable: false);
        var child = await CreateAsync(c, $"B{t}1", $"Bal child {t}", "asset", root);
        var income = await CreateAsync(c, $"B{t}2", $"Bal income {t}", "income");

        async Task PostAsync(string entity, decimal amount, string status = "post")
        {
            var j = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", new
            {
                legalEntityId = entity, date = "2026-05-01",
                lines = new[] { new { accountId = child, debit = amount, credit = 0m }, new { accountId = income, debit = 0m, credit = amount } },
            }), HttpStatusCode.Created);
            if (status == "post") await Ok(await c.PostAsync($"/api/v1/finance/journals/{(string)j["id"]!}/post", null));
        }
        await PostAsync(a, 300m);
        await PostAsync(b, 50m);
        await PostAsync(a, 999m, status: "draft"); // drafts never count

        decimal? Bal(JsonNode list, string id) => (decimal?)list.AsArray().Single(x => (string)x!["id"]! == id)!["balance"];
        var inA = await Ok(await c.GetAsync($"{Base}/tree/categories/asset/accounts?search=B{t}&includeBalances=true&legalEntityId={a}"));
        Assert.Equal(300m, Bal(inA, root)); // rolled up from the child
        var inB = await Ok(await c.GetAsync($"{Base}/{root}/children?includeBalances=true&legalEntityId={b}"));
        Assert.Equal(50m, Bal(inB, child));
        var incomeInA = await Ok(await c.GetAsync($"{Base}/tree/categories/income/accounts?search=B{t}&includeBalances=true&legalEntityId={a}"));
        Assert.Equal(300m, Bal(incomeInA, income)); // credit-natured, shown positive

        // The X-Entity-Id header narrows the balances the same way.
        var scoped = await AdminAsync();
        scoped.DefaultRequestHeaders.Add("X-Entity-Id", b);
        Assert.Equal(50m, Bal(await Ok(await scoped.GetAsync($"{Base}/tree/categories/asset/accounts?search=B{t}&includeBalances=true")), root));

        var cats = (await Ok(await c.GetAsync($"{Base}/tree/categories?includeBalances=true&legalEntityId={a}"))).AsArray();
        Assert.Equal(300m, (decimal)cats.Single(x => (string)x!["type"]! == "asset")!["balance"]!);
        Assert.Null((await Ok(await c.GetAsync($"{Base}/tree/categories/asset/accounts?search=B{t}")))[0]!["balance"]);

        // Chart readers without report access get the tree but no balances; no chart access at all is forbidden.
        var reader = await UserWithAsync(c, "finance.accounts.read");
        Assert.Null((await Ok(await reader.GetAsync($"{Base}/tree/categories/asset/accounts?search=B{t}&includeBalances=true")))[0]!["balance"]);
        Assert.Null((await Ok(await reader.GetAsync($"{Base}/tree/categories?includeBalances=true")))[0]!["balance"]);
        var outsider = await UserWithAsync(c, "core.parties.read");
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{Base}/tree/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{Base}/tree/categories/asset/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{Base}/{root}/children")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{Base}/{root}/path")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Base}/tree/categories")).StatusCode);
    }
}
