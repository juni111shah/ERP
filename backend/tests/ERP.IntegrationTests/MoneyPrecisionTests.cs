using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Money inputs are held to the cent, and spreading a receipt over invoices never leaves a sub-cent remainder.</summary>
public class MoneyPrecisionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string Vendor, string Bank);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Money " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        return new Setup(c, entity, customer, vendor, bank);
    }

    [Fact]
    public async Task A_receipt_with_three_decimals_is_rejected()
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 10.005m });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("2 decimal", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_payment_with_three_decimals_is_rejected()
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 20.123m });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task An_allocation_with_three_decimals_is_rejected()
    {
        var s = await NewAsync();
        var invoiceId = await IssueAsync(s, 100m);
        var res = await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 50m,
            allocations = new[] { new { invoiceId, amount = 49.999m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Auto_allocation_of_an_awkward_amount_leaves_exact_cents()
    {
        var s = await NewAsync();
        var ids = new[] { await IssueAsync(s, 33.33m), await IssueAsync(s, 33.33m), await IssueAsync(s, 33.33m) };
        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-12", method = "bankTransfer", bankAccountId = s.Bank, amount = 100m, autoAllocate = true,
        }), HttpStatusCode.Created);
        Assert.Equal(0.01m, (decimal)receipt["unallocated"]!);
        foreach (var id in ids)
        {
            var inv = await Ok(await s.C.GetAsync($"{Fin}/invoices/{id}"));
            Assert.Equal(("paid", 33.33m), ((string)inv["status"]!, (decimal)inv["paid"]!));
        }
    }

    private async Task<string> IssueAsync(Setup s, decimal price)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new
        {
            legalEntityId = s.Entity, partyId = s.Customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Service", quantity = 1m, price } },
        }), HttpStatusCode.Created);
        var issued = await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));
        return (string)issued["id"]!;
    }
}
