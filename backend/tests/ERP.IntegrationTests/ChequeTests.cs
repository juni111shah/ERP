using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The received-cheque register: custody, deposit, clearing, bounce and replacement, with the ledger and invoices following each step.</summary>
public class ChequeTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Party, string Bank, string InvoiceId);

    private async Task<Setup> NewAsync(decimal invoiceTotal = 1000m)
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "PDC " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var party = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Payer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = party, invoiceDate = "2026-03-01", lines = new[] { new { description = "Rent", quantity = 1m, price = invoiceTotal } } }), HttpStatusCode.Created);
        var invoice = (string)(await Ok(await c.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)))["id"]!;
        return new Setup(c, entity, party, bank, invoice);
    }

    private static object Cheque(Setup s, string no, string chequeDate = "2026-03-05", decimal amount = 1000m, string? invoiceId = "use")
        => new
        {
            legalEntityId = s.Entity, partyId = s.Party, kind = "postDatedCheque", instrumentNo = no, bankName = "Emirates NBD", amount, instrumentDate = chequeDate, receivedOn = "2026-03-02",
            allocations = invoiceId is null ? Array.Empty<object>() : new object[] { new { invoiceId = invoiceId == "use" ? s.InvoiceId : invoiceId, amount } },
        };

    private static async Task<decimal> ClosingAsync(Setup s, string code)
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={s.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    [Fact]
    public async Task A_cheque_settles_the_invoice_then_clears_into_the_bank()
    {
        var s = await NewAsync();
        var cheque = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "100001", "2026-03-20")), HttpStatusCode.Created);
        var id = (string)cheque["id"]!;
        Assert.StartsWith("CHQ-", (string)cheque["number"]!);
        Assert.Equal("inCustody", (string)cheque["status"]!);
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}")))["status"]!);
        Assert.Equal(1000m, await ClosingAsync(s, "1150")); // cheques in hand
        Assert.Equal(0m, await ClosingAsync(s, "1100"));    // receivables cleared

        // post-dated: not before the date on the cheque
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/deposit", new { bankAccountId = s.Bank, date = "2026-03-10" })).StatusCode);
        Assert.Equal("deposited", (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/deposit", new { bankAccountId = s.Bank, date = "2026-03-20" })))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/cancel", new { })).StatusCode);

        var cleared = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/clear", new { date = "2026-03-22" }));
        Assert.Equal("cleared", (string)cleared["status"]!);
        Assert.Equal(0m, await ClosingAsync(s, "1150"));
        Assert.Equal(1000m, await ClosingAsync(s, "1010")); // bank
    }

    [Fact]
    public async Task A_bounced_cheque_reopens_the_invoice_and_can_be_replaced()
    {
        var s = await NewAsync();
        var first = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "200001")), HttpStatusCode.Created);
        var id = (string)first["id"]!;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/deposit", new { bankAccountId = s.Bank, date = "2026-03-06" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "" })).StatusCode);

        var bounced = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/bounce", new { date = "2026-03-08", reason = "Insufficient funds" }));
        Assert.Equal(("bounced", "Insufficient funds"), ((string)bounced["status"]!, (string)bounced["bounceReason"]!));
        var invoice = await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}"));
        Assert.Equal(("issued", 1000m), ((string)invoice["status"]!, (decimal)invoice["outstanding"]!));
        Assert.Equal(0m, await ClosingAsync(s, "1150"));
        Assert.Equal(1000m, await ClosingAsync(s, "1100"));

        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/clear", new { date = "2026-03-09" })).StatusCode);
        var replacement = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/replace", Cheque(s, "200002")));
        Assert.Equal(id, (string)replacement["replacesInstrumentId"]!);
        Assert.Equal("replaced", (string)(await Ok(await s.C.GetAsync($"{Fin}/cheques/{id}")))["status"]!);
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{s.InvoiceId}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{id}/replace", Cheque(s, "200003"))).StatusCode); // already replaced
    }

    [Fact]
    public async Task Duplicate_cheques_are_refused_and_a_cheque_in_custody_can_be_cancelled()
    {
        var s = await NewAsync();
        var cheque = await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "300001", invoiceId: null)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "300001", invoiceId: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "300002", amount: 5000m))).StatusCode); // allocation larger than the invoice

        var cancelled = await Ok(await s.C.PostAsync($"{Fin}/cheques/{(string)cheque["id"]!}/cancel", null));
        Assert.Equal("cancelled", (string)cancelled["status"]!);
        Assert.Equal(0m, await ClosingAsync(s, "1150"));
        // the number can be used again once the first entry is cancelled
        Assert.Equal(HttpStatusCode.Created, (await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "300001", invoiceId: null))).StatusCode);
    }

    [Fact]
    public async Task The_register_lists_filters_and_summarises_cheques()
    {
        var s = await NewAsync();
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "400001", "2026-04-01", 600m)), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques", Cheque(s, "400002", "2026-04-20", 400m)), HttpStatusCode.Created);

        var listed = await Ok(await s.C.GetAsync($"{Fin}/cheques?filter=partyId:eq:{s.Party}&sort=instrumentDate"));
        Assert.Equal(new[] { "400001", "400002" }, listed.AsArray().Select(r => (string)r!["instrumentNo"]!).ToArray());
        Assert.Single((await Ok(await s.C.GetAsync($"{Fin}/cheques?search=400002"))).AsArray());

        var summary = await Ok(await s.C.GetAsync($"{Fin}/cheques/summary?asOf=2026-04-05"));
        Assert.True((decimal)summary["inCustodyTotal"]! >= 1000m);
        Assert.True((decimal)summary["maturedTotal"]! >= 600m);
        Assert.True((decimal)summary["next30DaysTotal"]! >= 400m);
    }
}
