using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Vendor bills, payments, issued cheques, approval rules and the payables ageing.</summary>
public class PayablesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly object VoidReason = new { reason = "Entered in error" };
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Vendor, string Bank, string Expense, string RecoverableTax, string NonRecoverableTax);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "AP " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Id(string code) => (string)accounts.First(a => (string?)a!["code"] == code)!["id"]!;
        async Task<string> Tax(bool recoverable) => (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "T" + Guid.NewGuid().ToString("N")[..6], name = "VAT", kind = "vat", rate = 5, isRecoverable = recoverable }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, vendor, Id("1010"), Id("6900"), await Tax(true), await Tax(false));
    }

    private static object Bill(Setup s, decimal price, string reference, string date = "2026-04-01", string? tax = "rec", string? due = null)
        => new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, vendorReference = reference, billDate = date, dueDate = due,
            lines = new[] { new { description = "Office supplies", quantity = 2m, price, accountId = s.Expense, taxCodeId = tax == "rec" ? s.RecoverableTax : tax == "non" ? s.NonRecoverableTax : null } },
        };

    private static async Task<string> PostedAsync(Setup s, object body)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", body), HttpStatusCode.Created);
        var posted = await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)draft["id"]!}/post", null));
        Assert.Equal("issued", (string)posted["status"]!);
        return (string)posted["id"]!;
    }

    private static async Task<decimal> ClosingAsync(Setup s, string code)
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={s.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    [Fact]
    public async Task A_bill_posts_expense_input_tax_and_payables_and_rejects_a_repeated_vendor_reference()
    {
        var s = await NewAsync();
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, 500m, "V-100")), HttpStatusCode.Created);
        Assert.Equal((1000m, 50m, 1050m), ((decimal)draft["net"]!, (decimal)draft["tax"]!, (decimal)draft["total"]!));
        Assert.Equal("2026-05-01", (string)draft["dueDate"]!);
        Assert.StartsWith("DRAFT-", (string)draft["number"]!);

        var posted = await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)draft["id"]!}/post", null));
        Assert.StartsWith("BILL-", (string)posted["number"]!);
        Assert.Equal(1000m, await ClosingAsync(s, "6900"));
        Assert.Equal(50m, await ClosingAsync(s, "1200"));
        Assert.Equal(-1050m, await ClosingAsync(s, "2000"));

        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, 10m, "V-100"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PutAsJsonAsync($"{Fin}/bills/{(string)draft["id"]!}", Bill(s, 1m, "V-100"))).StatusCode); // posted: no edits
    }

    [Fact]
    public async Task Tax_that_cannot_be_recovered_becomes_part_of_the_expense()
    {
        var s = await NewAsync();
        await PostedAsync(s, Bill(s, 100m, "V-200", tax: "non")); // 200 + 10 tax
        Assert.Equal(210m, await ClosingAsync(s, "6900"));
        Assert.Equal(0m, await ClosingAsync(s, "1200"));
        Assert.Equal(-210m, await ClosingAsync(s, "2000"));
    }

    [Fact]
    public async Task Payments_settle_bills_and_voiding_reopens_them()
    {
        var s = await NewAsync();
        var billId = await PostedAsync(s, Bill(s, 500m, "V-300", tax: null)); // 1000
        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-04-20", method = "bankTransfer", bankAccountId = s.Bank, amount = 600m, allocations = new[] { new { billId, amount = 600m } },
        }), HttpStatusCode.Created);
        Assert.StartsWith("PAY-", (string)payment["number"]!);
        var bill = await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal(("partlyPaid", 400m), ((string)bill["status"]!, (decimal)bill["outstanding"]!));
        Assert.Equal(-600m, await ClosingAsync(s, "1010"));

        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments", new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-04-21", method = "cash", bankAccountId = s.Bank, amount = 500m, allocations = new[] { new { billId, amount = 500m } },
        })).StatusCode);

        var advance = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-04-22", method = "bankTransfer", bankAccountId = s.Bank, amount = 400m }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments/{(string)advance["id"]!}/allocate", new { allocations = new[] { new { billId, amount = 400m } } }));
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/bills/{billId}/void", VoidReason)).StatusCode);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments/{(string)payment["id"]!}/void", VoidReason));
        var after = await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal(("partlyPaid", 600m), ((string)after["status"]!, (decimal)after["outstanding"]!));

        var listed = await Ok(await s.C.GetAsync($"{Fin}/payments?filter=partyId:eq:{s.Vendor}"));
        Assert.Equal(2, listed.AsArray().Count); // only payments, never customer receipts
    }

    [Fact]
    public async Task An_issued_cheque_credits_cheques_issued_until_it_clears()
    {
        var s = await NewAsync();
        var billId = await PostedAsync(s, Bill(s, 250m, "V-400", tax: null)); // 500
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-05-01", method = "instrument", bankAccountId = s.Bank, amount = 500m })).StatusCode); // no cheque number

        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-05-01", method = "instrument", bankAccountId = s.Bank, amount = 500m, chequeNo = "900001", chequeDate = "2026-05-15", bankName = "ADCB",
            allocations = new[] { new { billId, amount = 500m } },
        }), HttpStatusCode.Created);
        Assert.Equal(-500m, await ClosingAsync(s, "2150")); // cheques issued
        Assert.Equal(0m, await ClosingAsync(s, "1010"));    // the bank is not touched yet

        var chequeId = (string)payment["paymentInstrumentId"]!;
        var cheque = await Ok(await s.C.GetAsync($"{Fin}/cheques/{chequeId}"));
        Assert.Equal(("issued", "issued"), (((string)cheque["status"]!), ((string)cheque["direction"]!).ToLowerInvariant()));
        Assert.Equal("postDatedCheque", (string)cheque["kind"]!);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date = "2026-05-16" }));
        Assert.Equal(0m, await ClosingAsync(s, "2150"));
        Assert.Equal(-500m, await ClosingAsync(s, "1010"));
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/payments/{(string)payment["id"]!}/void", VoidReason)).StatusCode); // cleared: too late
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date = "2026-05-17" })).StatusCode);
    }

    [Fact]
    public async Task Voiding_an_unpaid_bill_reverses_the_ledger()
    {
        var s = await NewAsync();
        var billId = await PostedAsync(s, Bill(s, 100m, "V-500"));
        Assert.Equal(HttpStatusCode.OK, (await s.C.PostAsJsonAsync($"{Fin}/bills/{billId}/void", VoidReason)).StatusCode);
        Assert.Equal(0m, await ClosingAsync(s, "2000"));
        Assert.Equal(0m, await ClosingAsync(s, "6900"));
        // the reference can be used again after a void
        Assert.Equal(HttpStatusCode.Created, (await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, 100m, "V-500"))).StatusCode);
        var voided = await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal("Entered in error", (string?)voided["voidReason"]);
        Assert.NotNull(voided["voidedAt"]);
    }

    [Fact]
    public async Task A_bill_can_carry_a_business_unit_of_its_legal_entity()
    {
        var s = await NewAsync();
        var unit = (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = s.Entity, code = "B" + Guid.NewGuid().ToString("N")[..6], name = "Branch" }), HttpStatusCode.Created))["id"]!;
        object WithUnit(string reference, object? unitId) => new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, vendorReference = reference, billDate = "2026-04-01", businessUnitId = unitId,
            lines = new[] { new { description = "Rent", quantity = 1m, price = 100m, accountId = s.Expense } },
        };
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", WithUnit("BU-1", unit)), HttpStatusCode.Created);
        Assert.Equal(unit, (string?)draft["businessUnitId"]);
        Assert.Equal(unit, (string?)(await Ok(await s.C.GetAsync($"{Fin}/bills/{(string)draft["id"]!}")))["businessUnitId"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bills", WithUnit("BU-2", Guid.NewGuid()))).StatusCode);
        var unknownItem = new
        {
            legalEntityId = s.Entity, partyId = s.Vendor, vendorReference = "BU-3", billDate = "2026-04-01",
            lines = new[] { new { description = "X", quantity = 1m, price = 1m, accountId = s.Expense, itemId = Guid.NewGuid() } },
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bills", unknownItem)).StatusCode);
    }

    [Fact]
    public async Task Ageing_buckets_payables_by_due_date_and_nets_vendor_advances()
    {
        var s = await NewAsync();
        await PostedAsync(s, Bill(s, 100m, "A-1", "2026-01-01", null, "2026-02-01")); // 200, over 90 days late on 1 June
        await PostedAsync(s, Bill(s, 50m, "A-2", "2026-05-01", null, "2026-05-20"));  // 100, 12 days late
        await PostedAsync(s, Bill(s, 25m, "A-3", "2026-05-25", null, "2026-06-20"));  // 50, current
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-05-30", method = "cash", bankAccountId = s.Bank, amount = 30m }), HttpStatusCode.Created);

        var report = await Ok(await s.C.GetAsync($"{Fin}/reports/ap-ageing?asOf=2026-06-01&legalEntityId={s.Entity}"));
        var row = report["rows"]!.AsArray().Single(r => (string)r!["partyId"]! == s.Vendor)!;
        Assert.Equal((200m, 100m, 20m, 320m), ((decimal)row["over90"]!, (decimal)row["days1To30"]!, (decimal)row["current"]!, (decimal)row["total"]!));
        var statement = await Ok(await s.C.GetAsync($"{Fin}/reports/vendor-statement?partyId={s.Vendor}&asOf=2026-06-01&legalEntityId={s.Entity}"));
        Assert.Equal(320m, (decimal)statement["balance"]!);
    }

    [Fact]
    public async Task A_bill_over_the_policy_amount_waits_for_another_user_to_approve_it()
    {
        var s = await NewAsync();
        var policy = await Ok(await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "bill", minAmount = 5_000m }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "unknown" })).StatusCode);

        var small = await PostedAsync(s, Bill(s, 100m, "P-1", tax: null)); // under the threshold: posts straight away
        Assert.NotNull(small);

        var big = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, 3_000m, "P-2", tax: null)), HttpStatusCode.Created); // 6000
        var pending = await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)big["id"]!}/post", null));
        Assert.Equal("pendingApproval", (string)pending["status"]!);
        var requestId = (string)pending["approvalRequestId"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/bills/{(string)big["id"]!}/post", null)).StatusCode); // still waiting

        var roles = await Ok(await s.C.GetAsync("/api/v1/roles"));
        var approverRole = (string)roles.AsArray().First(r => (string?)r!["name"] == "Approver")!["id"]!;
        var email = $"ap{Guid.NewGuid():N}@demo.local"[..24] + "@demo.local";
        (await s.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Approver", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { approverRole } })).EnsureSuccessStatusCode();
        var approver = (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
        await Ok(await approver.PostAsJsonAsync($"/api/v1/approvals/{requestId}/approve", new { comment = "ok" }));

        var posted = await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)big["id"]!}/post", null));
        Assert.Equal("issued", (string)posted["status"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"/api/v1/approval-policies/{(string)policy["id"]!}")).StatusCode);
    }
}
