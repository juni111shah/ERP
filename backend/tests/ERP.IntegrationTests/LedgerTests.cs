using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Journals, posting rules, period lock and the ledger reports. Each test works in its own legal entity so totals are exact.</summary>
public class LedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static async Task<string> NewEntityAsync(HttpClient c)
        => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Ledger " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    private static async Task<Dictionary<string, string>> AccountsAsync(HttpClient c)
    {
        var page = await Ok(await c.GetAsync("/api/v1/finance/accounts?pageSize=200"));
        return page.AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
    }

    private static object Journal(string entity, string date, string debit, string credit, decimal amount, string? memo = null)
        => new { legalEntityId = entity, date, memo, lines = new[] { new { accountId = debit, debit = amount, credit = 0m }, new { accountId = credit, debit = 0m, credit = amount } } };

    [Fact]
    public async Task A_draft_journal_is_edited_posted_and_numbered_only_on_posting()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var (cash, income) = (acc.First(a => a.Key.StartsWith('1')).Value, acc.First(a => a.Key.StartsWith('4')).Value);

        var draft = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-03-01", cash, income, 100m)), HttpStatusCode.Created);
        Assert.StartsWith("DRAFT-", (string)draft["number"]!);
        var id = (string)draft["id"]!;

        var edited = await Ok(await c.PutAsJsonAsync($"/api/v1/finance/journals/{id}", Journal(entity, "2026-03-02", cash, income, 250m, "Edited")));
        Assert.Equal(250m, (decimal)edited["amount"]!);
        Assert.Equal(2, edited["lines"]!.AsArray().Count);

        var posted = await Ok(await c.PostAsync($"/api/v1/finance/journals/{id}/post", null));
        Assert.Equal("posted", (string)posted["status"]!);
        Assert.StartsWith("JV-", (string)posted["number"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/v1/finance/journals/{id}", Journal(entity, "2026-03-02", cash, income, 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"/api/v1/finance/journals/{id}")).StatusCode);
    }

    [Fact]
    public async Task An_unbalanced_or_one_sided_journal_is_rejected_and_a_draft_can_be_deleted()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var (a, b) = (acc.Values.First(), acc.Values.Last());

        var unbalanced = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", new { legalEntityId = entity, date = "2026-03-01", lines = new[] { new { accountId = a, debit = 100m, credit = 0m }, new { accountId = b, debit = 0m, credit = 90m } } }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/v1/finance/journals/{(string)unbalanced["id"]!}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/finance/journals/{(string)unbalanced["id"]!}")).StatusCode);

        var twoSided = await c.PostAsJsonAsync("/api/v1/finance/journals", new { legalEntityId = entity, date = "2026-03-01", lines = new[] { new { accountId = a, debit = 100m, credit = 100m }, new { accountId = b, debit = 0m, credit = 0m } } });
        Assert.Equal(HttpStatusCode.BadRequest, twoSided.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/finance/journals", new { legalEntityId = entity, date = "2026-03-01", lines = new[] { new { accountId = a, debit = 100m, credit = 0m } } })).StatusCode);
    }

    [Fact]
    public async Task Reversal_posts_the_opposite_entry_and_marks_the_original()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var (cash, income) = (acc.First(a => a.Key.StartsWith('1')).Value, acc.First(a => a.Key.StartsWith('4')).Value);
        var draft = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-04-01", cash, income, 400m)), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        await Ok(await c.PostAsync($"/api/v1/finance/journals/{id}/post", null));

        var reversal = await Ok(await c.PostAsJsonAsync($"/api/v1/finance/journals/{id}/reverse", new { memo = "Mistake" }));
        Assert.Equal(id, (string)reversal["reversalOfId"]!);
        Assert.Equal("posted", (string)reversal["status"]!);
        Assert.Equal("reversed", (string)(await Ok(await c.GetAsync($"/api/v1/finance/journals/{id}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/v1/finance/journals/{id}/reverse", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"/api/v1/finance/journals/{(string)reversal["id"]!}/reverse", new { })).StatusCode);

        var tb = await Ok(await c.GetAsync($"/api/v1/finance/reports/trial-balance?to=2026-12-31&legalEntityId={entity}"));
        Assert.All(tb["rows"]!.AsArray(), r => Assert.Equal(0m, (decimal)r!["closing"]!)); // original + reversal net to nothing
    }

    [Fact]
    public async Task Locked_books_refuse_posting_on_or_before_the_lock_date()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var (cash, income) = (acc.First(a => a.Key.StartsWith('1')).Value, acc.First(a => a.Key.StartsWith('4')).Value);

        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/v1/legal-entities/{entity}/books-lock", new { through = "2026-01-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/v1/legal-entities/{entity}/books-lock", new { through = "2099-01-31" })).StatusCode);

        var locked = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-01-15", cash, income, 10m)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"/api/v1/finance/journals/{(string)locked["id"]!}/post", null)).StatusCode);
        var open = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-02-01", cash, income, 10m)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/v1/finance/journals/{(string)open["id"]!}/post", null)).StatusCode);

        await c.PutAsJsonAsync($"/api/v1/legal-entities/{entity}/books-lock", new { through = (string?)null }); // unlock
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/v1/finance/journals/{(string)locked["id"]!}/post", null)).StatusCode);
    }

    [Fact]
    public async Task Reports_follow_posted_entries_only_and_the_balance_sheet_balances()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var cash = acc.First(a => a.Key.StartsWith('1')).Value;
        var income = acc.First(a => a.Key.StartsWith('4')).Value;
        var expense = acc.First(a => a.Key.StartsWith('6')).Value;
        var equity = acc.First(a => a.Key.StartsWith('3')).Value;

        async Task PostAsync(string date, string dr, string cr, decimal amount)
        {
            var j = await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, date, dr, cr, amount)), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"/api/v1/finance/journals/{(string)j["id"]!}/post", null));
        }
        await PostAsync("2026-01-05", cash, equity, 1000m);   // capital
        await PostAsync("2026-02-10", cash, income, 600m);    // sale
        await PostAsync("2026-02-20", expense, cash, 150m);   // cost
        await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-02-25", cash, income, 9999m)), HttpStatusCode.Created); // draft: ignored

        var tb = await Ok(await c.GetAsync($"/api/v1/finance/reports/trial-balance?from=2026-02-01&to=2026-02-28&legalEntityId={entity}"));
        Assert.Equal(750m, (decimal)tb["totalDebit"]!);
        Assert.Equal(750m, (decimal)tb["totalCredit"]!);
        var cashRow = tb["rows"]!.AsArray().Single(r => (string)r!["accountId"]! == cash)!;
        Assert.Equal(1000m, (decimal)cashRow["opening"]!);
        Assert.Equal(1450m, (decimal)cashRow["closing"]!);

        var gl = await Ok(await c.GetAsync($"/api/v1/finance/reports/general-ledger?accountId={cash}&to=2026-12-31&legalEntityId={entity}"));
        Assert.Equal(3, gl["lines"]!.AsArray().Count);
        Assert.Equal(1450m, (decimal)gl["closing"]!);

        var pl = await Ok(await c.GetAsync($"/api/v1/finance/reports/income-statement?from=2026-01-01&to=2026-12-31&legalEntityId={entity}"));
        Assert.Equal((600m, 150m, 450m), ((decimal)pl["totalIncome"]!, (decimal)pl["totalExpenses"]!, (decimal)pl["netProfit"]!));

        var bs = await Ok(await c.GetAsync($"/api/v1/finance/reports/balance-sheet?asOf=2026-12-31&legalEntityId={entity}"));
        Assert.Equal(1450m, (decimal)bs["totalAssets"]!);
        Assert.Equal(450m, (decimal)bs["currentEarnings"]!);
        Assert.True((bool)bs["balanced"]!);
    }

    [Fact]
    public async Task Journals_are_listed_and_filtered_through_the_generic_list()
    {
        var c = await AdminAsync();
        var entity = await NewEntityAsync(c);
        var acc = await AccountsAsync(c);
        var (cash, income) = (acc.First(a => a.Key.StartsWith('1')).Value, acc.First(a => a.Key.StartsWith('4')).Value);
        await Ok(await c.PostAsJsonAsync("/api/v1/finance/journals", Journal(entity, "2026-05-01", cash, income, 5m, "findme-xyz")), HttpStatusCode.Created);
        var list = await Ok(await c.GetAsync("/api/v1/finance/journals?search=findme-xyz"));
        Assert.Single(list.AsArray());
        Assert.Equal("draft", (string)list[0]!["status"]!);
        Assert.Empty((await Ok(await c.GetAsync("/api/v1/finance/journals?filter=status:eq:posted&search=findme-xyz"))).AsArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client().GetAsync("/api/v1/finance/journals")).StatusCode == HttpStatusCode.Unauthorized ? HttpStatusCode.Forbidden : HttpStatusCode.OK);
    }
}
