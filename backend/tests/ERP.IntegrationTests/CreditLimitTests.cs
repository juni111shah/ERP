using System.Net;
using System.Net.Http.Json;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>A customer's credit limit, checked when an order is confirmed and when an invoice is issued.</summary>
public class CreditLimitTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<string> CustomerAsync(SupplyChainKit k, decimal? limit)
        => (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/parties", new { name = "Limited " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", creditLimit = limit }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> OrderAsync(SupplyChainKit k, string customer, decimal price)
        => (string)(await Ok(await k.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = k.Entity, customerPartyId = customer, customerName = "Customer", orderDate = "2026-06-01", currency = "AED",
            lines = new[] { new { itemCode = "SVC", description = "Service", quantity = 1m, unitPrice = price } },
        }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> DraftInvoiceAsync(SupplyChainKit k, string customer, decimal price)
        => (string)(await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = k.Entity, partyId = customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Service", quantity = 1m, price } } }), HttpStatusCode.Created))["id"]!;

    [Fact]
    public async Task The_limit_is_a_field_on_the_party_and_cannot_be_negative()
    {
        var k = await NewAsync(f);
        var id = await CustomerAsync(k, 1000m);
        Assert.Equal(1000m, (decimal)(await Ok(await k.C.GetAsync($"/api/v1/parties/{id}")))["creditLimit"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync("/api/v1/parties", new { name = "Bad", kind = "Company", roles = "Customer", creditLimit = -1m })).StatusCode);
        var updated = await Ok(await k.C.PutAsJsonAsync($"/api/v1/parties/{id}", new { name = "Renamed", kind = "Company", roles = "Customer", creditLimit = 2500m }));
        Assert.Equal(2500m, (decimal)updated["creditLimit"]!);
    }

    [Fact]
    public async Task Confirming_an_order_over_the_limit_is_refused_and_nothing_changes()
    {
        var k = await NewAsync(f);
        var customer = await CustomerAsync(k, 1000m);
        var over = await OrderAsync(k, customer, 1500m);

        var res = await k.C.PutAsJsonAsync($"{Sales}/orders/{over}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("credit limit", await res.Content.ReadAsStringAsync());
        Assert.Equal("draft", (string)(await k.SaleAsync(over))["status"]!, ignoreCase: true);

        var within = await OrderAsync(k, customer, 1000m);
        await Ok(await k.C.PutAsJsonAsync($"{Sales}/orders/{within}/status", new { status = "Confirmed" }));

        // the confirmed order uses up the limit for the next one
        var next = await OrderAsync(k, customer, 1m);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Sales}/orders/{next}/status", new { status = "Confirmed" })).StatusCode);
    }

    [Fact]
    public async Task Issuing_an_invoice_over_the_limit_is_refused_and_posts_nothing()
    {
        var k = await NewAsync(f);
        var customer = await CustomerAsync(k, 1000m);
        var first = await DraftInvoiceAsync(k, customer, 800m);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{first}/issue", null));
        Assert.Equal(800m, await k.BalanceAsync("ar.control"));

        var second = await DraftInvoiceAsync(k, customer, 300m);
        var res = await k.C.PostAsync($"{Fin}/invoices/{second}/issue", null);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("credit limit", await res.Content.ReadAsStringAsync());
        Assert.Equal(800m, await k.BalanceAsync("ar.control"));
        Assert.Equal("draft", (string)(await Ok(await k.C.GetAsync($"{Fin}/invoices/{second}")))["status"]!);

        // paying down the balance makes room
        var bank = (string)(await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["code"] == "1010" || (string?)a!["subcategory"] == "Bank")!["id"]!;
        await Ok(await k.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = k.Entity, partyId = customer, date = "2026-06-02", method = "BankTransfer", bankAccountId = bank, amount = 200m, autoAllocate = true }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{second}/issue", null));
        Assert.Equal(900m, await k.BalanceAsync("ar.control"));
    }

    [Fact]
    public async Task A_customer_with_no_limit_is_not_checked()
    {
        var k = await NewAsync(f);
        var customer = await CustomerAsync(k, null);
        var invoice = await DraftInvoiceAsync(k, customer, 1_000_000m);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{invoice}/issue", null));
        Assert.Equal(1_000_000m, await k.BalanceAsync("ar.control"));
    }
}

/// <summary>Warn-only mode. On its own host because the setting applies to the whole workspace.</summary>
public class CreditLimitWarnOnlyTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task With_warn_only_on_the_document_goes_through_and_carries_the_warning()
    {
        var k = await NewAsync(f);
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["core.creditLimitWarnOnly"] = true })).StatusCode);
        var customer = (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/parties", new { name = "Warned", kind = "Company", roles = "Customer", creditLimit = 100m }), HttpStatusCode.Created))["id"]!;

        var so = await Ok(await k.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = k.Entity, customerPartyId = customer, customerName = "Customer", orderDate = "2026-06-01", currency = "AED",
            lines = new[] { new { itemCode = "SVC", description = "Service", quantity = 1m, unitPrice = 500m } },
        }), HttpStatusCode.Created);
        var confirmed = await Ok(await k.C.PutAsJsonAsync($"{Sales}/orders/{(string)so["id"]!}/status", new { status = "Confirmed" }));
        Assert.Equal("confirmed", (string)confirmed["status"]!, ignoreCase: true);
        Assert.Contains("credit limit", (string)confirmed["warnings"]![0]!);

        var inv = await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = k.Entity, partyId = customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Service", quantity = 1m, price = 500m } } }), HttpStatusCode.Created);
        var issued = await Ok(await k.C.PostAsync($"{Fin}/invoices/{(string)inv["id"]!}/issue", null));
        Assert.Equal("issued", (string)issued["status"]!);
        Assert.Contains(issued["warnings"]!.AsArray(), w => ((string)w!).Contains("credit limit"));
        Assert.Equal(500m, await k.BalanceAsync("ar.control"));
    }
}
