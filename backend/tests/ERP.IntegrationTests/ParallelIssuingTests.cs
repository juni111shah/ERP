using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>SQL Server collection (needs ERP_TEST_SQL): concurrent numbered documents must all succeed with unique numbers (TASK-021).</summary>
public class ParallelIssuingTests
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [SqlServerFact]
    public async Task Migrations_apply_to_an_empty_sql_server_database_and_the_api_starts()
    {
        using var f = new SqlServerApiFactory();
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/me")).StatusCode);
    }

    [SqlServerFact]
    public async Task Twenty_parallel_receipts_all_succeed_with_distinct_numbers()
    {
        using var f = new SqlServerApiFactory();
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Par " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await ApiFactory.Json(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            var res = await c.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = entity, partyId = customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = bank, amount = 10m + i });
            return (res.StatusCode, Body: await res.Content.ReadAsStringAsync());
        }));

        Assert.All(results, r => Assert.True(r.StatusCode == HttpStatusCode.Created, $"{r.StatusCode}: {r.Body}"));
        var numbers = results.Select(r => (string)ApiFactory.Unwrap(JsonNode.Parse(r.Body)!)["number"]!).ToList();
        Assert.Equal(20, numbers.Distinct().Count());
    }

    [SqlServerFact]
    public async Task Ten_parallel_invoice_issues_all_succeed_with_distinct_numbers()
    {
        using var f = new SqlServerApiFactory();
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "ParInv " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;

        var ids = new List<string>();
        for (var i = 0; i < 10; i++)
            ids.Add((string)(await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = "2026-06-10", lines = new[] { new { description = "Work", quantity = 1m, price = 100m + i } } }), HttpStatusCode.Created))["id"]!);

        var results = await Task.WhenAll(ids.Select(async id =>
        {
            var res = await c.PostAsync($"{Fin}/invoices/{id}/issue", null);
            return (res.StatusCode, Body: await res.Content.ReadAsStringAsync());
        }));

        Assert.All(results, r => Assert.True(r.StatusCode == HttpStatusCode.OK, $"{r.StatusCode}: {r.Body}"));
        Assert.Equal(10, results.Select(r => (string)ApiFactory.Unwrap(JsonNode.Parse(r.Body)!)["number"]!).Distinct().Count());
    }

    [SqlServerFact]
    public async Task Ten_parallel_bill_posts_all_succeed_with_distinct_numbers()
    {
        using var f = new SqlServerApiFactory();
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "ParBill " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        var expense = (string)(await ApiFactory.Json(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["code"] == "6900")!["id"]!;

        var ids = new List<string>();
        for (var i = 0; i < 10; i++)
            ids.Add((string)(await Ok(await c.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = entity, partyId = vendor, vendorReference = "B-" + i, billDate = "2026-06-10", lines = new[] { new { description = "Work", quantity = 1m, price = 100m + i, accountId = expense } } }), HttpStatusCode.Created))["id"]!);

        var results = await Task.WhenAll(ids.Select(async id =>
        {
            var res = await c.PostAsync($"{Fin}/bills/{id}/post", null);
            return (res.StatusCode, Body: await res.Content.ReadAsStringAsync());
        }));

        Assert.All(results, r => Assert.True(r.StatusCode == HttpStatusCode.OK, $"{r.StatusCode}: {r.Body}"));
        Assert.Equal(10, results.Select(r => (string)ApiFactory.Unwrap(JsonNode.Parse(r.Body)!)["number"]!).Distinct().Count());
    }
}
