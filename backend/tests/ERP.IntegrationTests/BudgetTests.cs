using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Budgets and the budget-vs-actual report against the posted ledger.</summary>
public class BudgetTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Budget_vs_actual_compares_plan_with_posted_income_and_expense()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Bud " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Id(string code) => (string)accounts.First(a => (string?)a!["code"] == code)!["id"]!;
        var bank = Id("1010"); var expense = Id("6900"); var income = accounts.First(a => (string?)a!["type"] == "income")!["id"]!.ToString();

        var created = await Ok(await c.PostAsJsonAsync($"{Fin}/budgets", new
        {
            legalEntityId = entity, name = "FY2026 plan", fiscalYear = 2026, isActive = true,
            lines = new[]
            {
                new { accountId = expense, month = 1, amount = 1000m }, new { accountId = expense, month = 2, amount = 1000m },
                new { accountId = income, month = 1, amount = 5000m }, new { accountId = income, month = 2, amount = 5000m },
            },
        }), HttpStatusCode.Created);
        var id = (string)created["id"]!;
        Assert.Equal(10_000m, (decimal)created["totalIncome"]!);
        Assert.Equal(2_000m, (decimal)created["totalExpense"]!);

        async Task PostAsync(string date, string debit, string credit, decimal amount)
        {
            var j = await Ok(await c.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = entity, date, memo = "x", lines = new object[] { new { accountId = debit, debit = amount, credit = 0m }, new { accountId = credit, debit = 0m, credit = amount } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/journals/{(string)j["id"]!}/post", null));
        }
        await PostAsync("2026-01-15", bank, income, 4000m);   // income January
        await PostAsync("2026-02-10", expense, bank, 1500m);  // expense February
        await PostAsync("2025-12-31", expense, bank, 9999m);  // outside the year: ignored

        var report = await Ok(await c.GetAsync($"{Fin}/budgets/{id}/vs-actual?fromMonth=1&toMonth=2"));
        Assert.Equal(10_000m, (decimal)report["budgetIncome"]!);
        Assert.Equal(4_000m, (decimal)report["actualIncome"]!);
        Assert.Equal(2_000m, (decimal)report["budgetExpense"]!);
        Assert.Equal(1_500m, (decimal)report["actualExpense"]!);
        var exp = report["rows"]!.AsArray().Single(r => (string)r!["accountId"]! == expense)!;
        Assert.Equal(500m, (decimal)exp["variance"]!); // under budget = favourable
        var inc = report["rows"]!.AsArray().Single(r => (string)r!["accountId"]! == income)!;
        Assert.Equal(-6_000m, (decimal)inc["variance"]!);

        var janOnly = await Ok(await c.GetAsync($"{Fin}/budgets/{id}/vs-actual?fromMonth=1&toMonth=1"));
        Assert.Equal(0m, (decimal)janOnly["actualExpense"]!);
    }

    [Fact]
    public async Task Budgets_validate_accounts_duplicates_and_can_be_edited_and_deleted()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Bud " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var expense = (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!;
        var bank = (string)accounts.First(a => (string?)a!["code"] == "1010")!["id"]!;

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Fin}/budgets", new { legalEntityId = entity, name = "Bad", fiscalYear = 2026, lines = new[] { new { accountId = bank, month = 1, amount = 10m } } })).StatusCode); // asset account
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Fin}/budgets", new { legalEntityId = entity, name = "Dup", fiscalYear = 2026, lines = new[] { new { accountId = expense, month = 1, amount = 10m }, new { accountId = expense, month = 1, amount = 20m } } })).StatusCode);

        var b = await Ok(await c.PostAsJsonAsync($"{Fin}/budgets", new { legalEntityId = entity, name = "Edit me", fiscalYear = 2026, isActive = true, lines = new[] { new { accountId = expense, month = 3, amount = 100m } } }), HttpStatusCode.Created);
        var id = (string)b["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Fin}/budgets", new { legalEntityId = entity, name = "Edit me", fiscalYear = 2026, isActive = true, lines = Array.Empty<object>() })).StatusCode);
        var updated = await Ok(await c.PutAsJsonAsync($"{Fin}/budgets/{id}", new { legalEntityId = entity, name = "Edited", fiscalYear = 2026, isActive = true, lines = new[] { new { accountId = expense, month = 4, amount = 250m } } }));
        Assert.Equal("Edited", (string)updated["name"]!);
        Assert.Equal(250m, (decimal)updated["totalExpense"]!);
        Assert.Contains((await Ok(await c.GetAsync($"{Fin}/budgets?pageSize=100"))).AsArray(), x => (string)x!["id"]! == id);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Fin}/budgets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Fin}/budgets/{id}")).StatusCode);
    }
}
