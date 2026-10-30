using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Invoice and receipt input rules: customer-only parties, no forged source link, plausible dates and precision (TASK-126, 127, 129).</summary>
public class OrderToCashInputTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string VendorOnly, string EmployeeOnly, string Both, string Bank);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var tag = Guid.NewGuid().ToString("N")[..6];
        async Task<string> Party(string name, string roles)
            => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = name + " " + tag, kind = "Company", roles, paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Input " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await ApiFactory.Json(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        return new Setup(c, entity, await Party("Customer", "Customer"), await Party("Vendor", "Vendor"), await Party("Employee", "Employee"),
            await Party("Both", "Customer, Vendor"), bank);
    }

    private static Dictionary<string, object?> Invoice(Setup s, string party, bool credit = false, string date = "2026-06-01", string? due = null,
        decimal quantity = 1m, decimal price = 100m, bool forgedSource = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["legalEntityId"] = s.Entity, ["partyId"] = party, ["invoiceDate"] = date, ["dueDate"] = due, ["isCreditNote"] = credit,
            ["lines"] = new[] { new { description = "Service", quantity, price } },
        };
        if (forgedSource) { body["sourceType"] = "salesorder"; body["sourceId"] = Guid.NewGuid(); }
        return body;
    }

    private static object Receipt(Setup s, string party, string date = "2026-06-10")
        => new { legalEntityId = s.Entity, partyId = party, date, method = "bankTransfer", bankAccountId = s.Bank, amount = 100m };

    // ---- TASK-126

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_invoice_or_credit_note_for_a_vendor_or_employee_is_rejected(bool credit)
    {
        var s = await NewAsync();
        foreach (var party in new[] { s.VendorOnly, s.EmployeeOnly })
        {
            var res = await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, party, credit));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("partyId", await res.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task A_receipt_for_a_vendor_or_employee_is_rejected()
    {
        var s = await NewAsync();
        foreach (var party in new[] { s.VendorOnly, s.EmployeeOnly })
        {
            var res = await s.C.PostAsJsonAsync($"{Fin}/receipts", Receipt(s, party));
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("partyId", await res.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Customer_and_customer_vendor_parties_still_work()
    {
        var s = await NewAsync();
        foreach (var party in new[] { s.Customer, s.Both })
        {
            await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, party)), HttpStatusCode.Created);
            await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", Receipt(s, party)), HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task Updating_an_invoice_to_a_vendor_party_is_rejected()
    {
        var s = await NewAsync();
        var id = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer)), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PutAsJsonAsync($"{Fin}/invoices/{id}", Invoice(s, s.VendorOnly))).StatusCode);
    }

    // ---- TASK-127

    [Fact]
    public async Task A_forged_source_link_in_the_request_body_is_ignored()
    {
        var s = await NewAsync();
        var created = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer, forgedSource: true)), HttpStatusCode.Created);
        Assert.Null((string?)created["sourceType"]);
        Assert.Null((string?)created["sourceId"]);

        var id = (string)created["id"]!;
        var updated = await Ok(await s.C.PutAsJsonAsync($"{Fin}/invoices/{id}", Invoice(s, s.Customer, forgedSource: true)), HttpStatusCode.OK);
        Assert.Null((string?)updated["sourceType"]);
        Assert.Null((string?)updated["sourceId"]);
    }

    [Fact]
    public async Task A_credit_note_from_an_invoice_keeps_its_link()
    {
        var s = await NewAsync();
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer)), HttpStatusCode.Created);
        var id = (string)(await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null), HttpStatusCode.OK))["id"]!;
        var credit = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{id}/credit-note", new { }), HttpStatusCode.Created);
        Assert.Equal(id, (string)credit["sourceId"]!);
    }

    // ---- TASK-129

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("1999-12-31")]
    [InlineData("2099-01-01")]
    public async Task Implausible_invoice_and_receipt_dates_are_rejected(string date)
    {
        var s = await NewAsync();
        var invoice = await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer, date: date));
        Assert.Equal(HttpStatusCode.BadRequest, invoice.StatusCode);
        Assert.Contains("invoicedate", (await invoice.Content.ReadAsStringAsync()).ToLowerInvariant());

        var receipt = await s.C.PostAsJsonAsync($"{Fin}/receipts", Receipt(s, s.Customer, date));
        Assert.Equal(HttpStatusCode.BadRequest, receipt.StatusCode);
        Assert.Contains("date", (await receipt.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_due_date_before_the_invoice_date_or_far_ahead_is_rejected()
    {
        var s = await NewAsync();
        var before = await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer, date: "2026-06-10", due: "2026-06-01"));
        Assert.Equal(HttpStatusCode.BadRequest, before.StatusCode);
        Assert.Contains("duedate", (await before.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer, due: "2099-01-01"))).StatusCode);
    }

    [Theory]
    [InlineData("1.00001", "100", false)]
    [InlineData("1", "9.12345", false)]
    [InlineData("1.2345", "9.1234", true)]
    public async Task Quantity_and_price_allow_at_most_four_decimals(string quantity, string price, bool accepted)
    {
        var s = await NewAsync();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var res = await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, s.Customer, quantity: decimal.Parse(quantity, inv), price: decimal.Parse(price, inv)));
        Assert.Equal(accepted ? HttpStatusCode.Created : HttpStatusCode.BadRequest, res.StatusCode);
    }
}
