using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Bill input rules (dates, precision, active tax codes) and payment/cheque date rules (TASK-137, TASK-138).</summary>
public class BillPaymentInputTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Vendor, string Bank, string Expense, string ActiveTax, string InactiveTax);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "BP " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await ApiFactory.Json(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Id(string code) => (string)accounts.First(a => (string?)a!["code"] == code)!["id"]!;
        async Task<string> Tax(bool active) => (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "T" + Guid.NewGuid().ToString("N")[..6], name = "VAT", kind = "vat", rate = 5, isRecoverable = true, isActive = active }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, vendor, Id("1010"), Id("6900"), await Tax(true), await Tax(false));
    }

    private static Dictionary<string, object?> Bill(Setup s, string date = "2026-06-01", string? due = null, decimal quantity = 1m, decimal price = 100m, string? tax = null)
        => new()
        {
            ["legalEntityId"] = s.Entity, ["partyId"] = s.Vendor, ["vendorReference"] = "R-" + Guid.NewGuid().ToString("N")[..8], ["billDate"] = date, ["dueDate"] = due,
            ["lines"] = new[] { new { description = "Supplies", quantity, price, accountId = s.Expense, taxCodeId = tax } },
        };

    private static async Task<string> BodyAsync(HttpResponseMessage res) => (await res.Content.ReadAsStringAsync()).ToLowerInvariant();

    // ---- TASK-137

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2099-01-01")]
    public async Task A_bill_dated_in_year_1_or_2099_is_rejected_on_billDate(string date)
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, date));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("billdate", await BodyAsync(res));
    }

    [Fact]
    public async Task A_due_date_before_the_bill_date_is_rejected_on_dueDate()
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, "2026-06-10", "2026-06-01"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("duedate", await BodyAsync(res));
    }

    [Theory]
    [InlineData("1.00001", "100", false)]
    [InlineData("1", "9.12345", false)]
    [InlineData("1.2345", "9.1234", true)]
    public async Task Bill_quantity_and_price_allow_at_most_four_decimals(string quantity, string price, bool accepted)
    {
        var s = await NewAsync();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var res = await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, quantity: decimal.Parse(quantity, inv), price: decimal.Parse(price, inv)));
        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task An_inactive_tax_code_on_a_bill_line_is_rejected_and_an_active_one_works()
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, tax: s.InactiveTax));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("lines", await BodyAsync(res));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, tax: s.ActiveTax)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Updating_a_draft_bill_applies_the_same_rules()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s)), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PutAsJsonAsync($"{Fin}/bills/{id}", Bill(s, "0001-01-01"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.C.PutAsJsonAsync($"{Fin}/bills/{id}", Bill(s, "2026-06-02"))).StatusCode);
    }

    // ---- TASK-138

    private static object Payment(Setup s, string date, string? chequeDate = null, bool cheque = false)
        => new Dictionary<string, object?>
        {
            ["legalEntityId"] = s.Entity, ["partyId"] = s.Vendor, ["date"] = date, ["method"] = cheque ? "instrument" : "bankTransfer", ["bankAccountId"] = s.Bank, ["amount"] = 100m,
            ["chequeNo"] = cheque ? "C-" + Guid.NewGuid().ToString("N")[..6] : null, ["chequeDate"] = chequeDate,
        };

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("2099-01-01")]
    public async Task A_payment_dated_in_year_1_or_2099_is_rejected_on_date(string date)
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, date));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("date", await BodyAsync(res));
    }

    [Fact]
    public async Task A_cheque_dated_1999_is_rejected_on_chequeDate()
    {
        var s = await NewAsync();
        var res = await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, "2026-06-01", "1999-12-31", cheque: true));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("chequedate", await BodyAsync(res));
    }

    [Fact]
    public async Task A_normal_and_a_post_dated_cheque_payment_still_work()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, "2026-06-01")), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, "2026-06-01", "2026-07-01", cheque: true)), HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("0002-01-01")]
    [InlineData("2099-01-01")]
    public async Task Clearing_an_issued_cheque_on_an_implausible_date_is_rejected_on_date(string date)
    {
        var s = await NewAsync();
        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, "2026-06-01", "2026-06-15", cheque: true)), HttpStatusCode.Created);
        var chequeId = (string)payment["paymentInstrumentId"]!;
        var res = await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("date", await BodyAsync(res));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date = "2026-06-16" }), HttpStatusCode.OK);
    }
}
