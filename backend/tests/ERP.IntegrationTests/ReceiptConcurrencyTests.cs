using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>SQL Server collection (needs ERP_TEST_SQL): allocating and voiding receipts at the same moment keeps invoice balances consistent (TASK-128).</summary>
public class ReceiptConcurrencyTests
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string Bank);

    private static async Task<Setup> NewAsync(SqlServerApiFactory f)
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Rc " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await ApiFactory.Json(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        return new Setup(c, entity, customer, bank);
    }

    private static async Task<string> IssuedInvoiceAsync(Setup s, decimal price)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new
        {
            legalEntityId = s.Entity, partyId = s.Customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Work", quantity = 1m, price } },
        }), HttpStatusCode.Created);
        return (string)(await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null), HttpStatusCode.OK))["id"]!;
    }

    private static async Task<string> ReceiptAsync(Setup s, decimal amount, string? invoiceId = null)
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount,
            allocations = invoiceId is null ? null : new[] { new { invoiceId, amount } },
        }), HttpStatusCode.Created))["id"]!;

    [SqlServerFact]
    public async Task Two_parallel_allocations_never_overpay_an_invoice()
    {
        using var f = new SqlServerApiFactory();
        var s = await NewAsync(f);
        var invoice = await IssuedInvoiceAsync(s, 1000m);
        var receipts = new[] { await ReceiptAsync(s, 800m), await ReceiptAsync(s, 800m) };

        var results = await Task.WhenAll(receipts.Select(async r =>
        {
            var res = await s.C.PostAsJsonAsync($"{Fin}/receipts/{r}/allocate", new { allocations = new[] { new { invoiceId = invoice, amount = 800m } } });
            return (res.StatusCode, Body: await res.Content.ReadAsStringAsync());
        }));

        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{r.StatusCode}: {r.Body}"));
        var inv = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoice}"), HttpStatusCode.OK);
        var paid = (decimal)inv["paid"]!;
        Assert.True(paid <= (decimal)inv["total"]!, $"paid {paid} exceeds total");
        Assert.Equal(800m, paid); // exactly one allocation fits; the other is rejected because only 200 is left
    }

    [SqlServerFact]
    public async Task A_void_racing_an_allocation_ends_consistent()
    {
        using var f = new SqlServerApiFactory();
        var s = await NewAsync(f);
        var invoice = await IssuedInvoiceAsync(s, 1000m);
        var first = await ReceiptAsync(s, 400m, invoice);   // allocated: invoice paid 400
        var second = await ReceiptAsync(s, 300m);           // not allocated yet

        var voidTask = s.C.PostAsJsonAsync($"{Fin}/receipts/{first}/void", new { reason = "Entered in error" });
        var allocateTask = s.C.PostAsJsonAsync($"{Fin}/receipts/{second}/allocate", new { allocations = new[] { new { invoiceId = invoice, amount = 300m } } });
        var responses = await Task.WhenAll(voidTask, allocateTask);

        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        var allocated = responses[1].StatusCode == HttpStatusCode.OK;
        var inv = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoice}"), HttpStatusCode.OK);
        Assert.Equal(allocated ? 300m : 0m, (decimal)inv["paid"]!);
        Assert.Equal("voided", (string)(await Ok(await s.C.GetAsync($"{Fin}/receipts/{first}"), HttpStatusCode.OK))["status"]!);
    }
}
