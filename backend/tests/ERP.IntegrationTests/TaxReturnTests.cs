using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The country-pack tax return through the API, and the non-blocking tax-invoice warnings.</summary>
public class TaxReturnIntegrationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Customer, string Vendor, string Expense);

    private async Task<Ctx> NewAsync(string country, string currency)
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = $"{country} " + Guid.NewGuid().ToString("N")[..6], countryCode = country, baseCurrency = currency, isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Cust " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vend " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        return new Ctx(c, entity, customer, vendor, (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!);
    }

    private static async Task<string> TaxAsync(Ctx x, decimal rate, bool recoverable = true)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "T" + Guid.NewGuid().ToString("N")[..7], name = "Tax " + rate, kind = "vat", rate, isRecoverable = recoverable }), HttpStatusCode.Created))["id"]!;

    private static async Task<JsonNode> IssueAsync(Ctx x, string taxId, decimal price, string date)
    {
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = x.Customer, invoiceDate = date, lines = new[] { new { description = "Service", quantity = 1m, price, taxCodeId = taxId } } }), HttpStatusCode.Created);
        return await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));
    }

    private static async Task PostBillAsync(Ctx x, string taxId, decimal price, string date)
    {
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = x.Entity, partyId = x.Vendor, vendorReference = "B-" + Guid.NewGuid().ToString("N")[..6], billDate = date, lines = new[] { new { description = "Supplies", quantity = 1m, price, accountId = x.Expense, taxCodeId = taxId } } }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsync($"{Fin}/bills/{(string)draft["id"]!}/post", null));
    }

    private static decimal Box(JsonNode ret, string key) => (decimal)ret["rows"]!.AsArray().Single(r => (string?)r!["key"] == key)!["amount"]!;

    [Fact]
    public async Task The_uae_return_is_filled_from_issued_invoices_and_posted_bills()
    {
        var x = await NewAsync("AE", "AED");
        var std = await TaxAsync(x, 5m); var zero = await TaxAsync(x, 0m); var exempt = await TaxAsync(x, 0m, recoverable: false);
        await IssueAsync(x, std, 1000m, "2026-05-10"); await IssueAsync(x, zero, 400m, "2026-05-11"); await IssueAsync(x, exempt, 300m, "2026-05-12");
        await IssueAsync(x, std, 5000m, "2026-07-01"); // outside the quarter
        await PostBillAsync(x, std, 600m, "2026-05-15");

        var ret = await Ok(await x.C.GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30&legalEntityId={x.Entity}"));
        Assert.Equal("AE", (string)ret["countryCode"]!);
        Assert.Equal("VAT201", (string)ret["code"]!);
        Assert.Equal(1000m, Box(ret, "1_amount")); Assert.Equal(50m, Box(ret, "1_vat"));
        Assert.Equal(400m, Box(ret, "4_amount")); Assert.Equal(300m, Box(ret, "5_amount"));
        Assert.Equal(600m, Box(ret, "9_amount")); Assert.Equal(30m, Box(ret, "9_vat"));
        Assert.Equal(20m, (decimal)ret["netPayable"]!);
        Assert.Equal("AED", (string)ret["currencyCode"]!);
    }

    [Fact]
    public async Task The_uk_return_has_box_totals_and_credit_notes_reduce_box_1()
    {
        var x = await NewAsync("GB", "GBP");
        var vat = await TaxAsync(x, 20m);
        await IssueAsync(x, vat, 1000m, "2026-05-10");
        var credit = await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = x.Customer, invoiceDate = "2026-05-20", isCreditNote = true, lines = new[] { new { description = "Refund", quantity = 1m, price = 100m, taxCodeId = vat } } }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)credit["id"]!}/issue", null));
        await PostBillAsync(x, vat, 500m, "2026-05-15");

        var ret = await Ok(await x.C.GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30&legalEntityId={x.Entity}"));
        Assert.Equal(180m, Box(ret, "box1")); // 200 - 20
        Assert.Equal(100m, Box(ret, "box4"));
        Assert.Equal(80m, (decimal)ret["netPayable"]!);
        Assert.Equal(900m, Box(ret, "box6")); Assert.Equal(500m, Box(ret, "box7"));
    }

    [Fact]
    public async Task A_country_without_a_return_and_a_missing_entity_get_clear_errors()
    {
        var x = await NewAsync("FR", "EUR");
        var none = await x.C.GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30&legalEntityId={x.Entity}");
        Assert.Equal(HttpStatusCode.Conflict, none.StatusCode);
        Assert.Contains("FR", await none.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.C.GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30&legalEntityId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Fin}/reports/tax-return?from=2026-04-01&to=2026-06-30&legalEntityId={x.Entity}")).StatusCode);
    }

    [Fact]
    public async Task Issuing_a_tax_invoice_warns_about_missing_tax_numbers_but_never_blocks()
    {
        var x = await NewAsync("AE", "AED");
        var std = await TaxAsync(x, 5m);
        var issued = await IssueAsync(x, std, 100m, "2026-05-10");
        Assert.Equal("issued", (string)issued["status"]!);
        var warnings = issued["warnings"]!.AsArray().Select(w => (string)w!).ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("legal entity"));
        Assert.Contains(warnings, w => w.Contains("customer"));

        // no tax on the document: nothing is expected
        var plain = await Ok(await x.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = x.Entity, partyId = x.Customer, invoiceDate = "2026-05-11", lines = new[] { new { description = "No tax", quantity = 1m, price = 10m } } }), HttpStatusCode.Created);
        var plainIssued = await Ok(await x.C.PostAsync($"{Fin}/invoices/{(string)plain["id"]!}/issue", null));
        Assert.Empty(plainIssued["warnings"]!.AsArray());

        // a country that expects nothing extra on invoices
        var us = await NewAsync("US", "USD");
        var usTax = await TaxAsync(us, 7m);
        Assert.Empty((await IssueAsync(us, usTax, 100m, "2026-05-10"))["warnings"]!.AsArray());
    }
}
