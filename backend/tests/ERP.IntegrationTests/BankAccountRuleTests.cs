using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Receipts and payments may only use bank or cash accounts, never another asset account such as receivables.</summary>
public class BankAccountRuleTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string Vendor, string Bank, string Cash, string Receivables);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Bank rule " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Role(string role) => (string)accounts.First(a => (string?)a!["systemRole"] == role)!["id"]!;
        return new Setup(c, entity, customer, vendor, Role("bank"), Role("cash"), Role("ar.control"));
    }

    [Fact]
    public async Task A_receipt_cannot_use_a_non_bank_asset_account()
    {
        var s = await NewAsync();
        var bad = await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Receivables, amount = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("bankAccountId", await bad.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_payment_cannot_use_a_non_bank_asset_account()
    {
        var s = await NewAsync();
        var bad = await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Receivables, amount = 100m });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Bank_and_cash_accounts_are_still_accepted()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 100m }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Customer, date = "2026-06-11", method = "cash", bankAccountId = s.Cash, amount = 50m }), HttpStatusCode.Created);
    }
}
