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

    private static async Task<string> ApprovalStatusAsync(Setup s, string requestId)
        => ((string)(await Ok(await s.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray().Single(a => (string)a!["id"]! == requestId)!["status"]!).ToLowerInvariant();

    private static async Task<(string BillId, string RequestId)> PendingBillAsync(Setup s, string reference)
    {
        var big = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", Bill(s, 3_000m, reference, tax: null)), HttpStatusCode.Created); // 6000
        var pending = await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)big["id"]!}/post", null));
        Assert.Equal("pendingApproval", (string)pending["status"]!);
        return ((string)big["id"]!, (string)pending["approvalRequestId"]!);
    }

    [Fact]
    public async Task Editing_or_deleting_a_bill_withdraws_its_pending_approval()
    {
        var s = await NewAsync();
        var policy = await Ok(await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "bill", minAmount = 5_000m }), HttpStatusCode.Created);

        var (billId, requestId) = await PendingBillAsync(s, "W-1");
        Assert.Equal("pending", await ApprovalStatusAsync(s, requestId));
        await Ok(await s.C.PutAsJsonAsync($"{Fin}/bills/{billId}", Bill(s, 3_100m, "W-1", tax: null)));
        Assert.Equal("withdrawn", await ApprovalStatusAsync(s, requestId));
        var resubmitted = await Ok(await s.C.PostAsync($"{Fin}/bills/{billId}/post", null));
        Assert.Equal("pendingApproval", (string)resubmitted["status"]!);
        Assert.NotEqual(requestId, (string)resubmitted["approvalRequestId"]!);

        var (second, secondRequest) = await PendingBillAsync(s, "W-2");
        Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"{Fin}/bills/{second}")).StatusCode);
        Assert.Equal("withdrawn", await ApprovalStatusAsync(s, secondRequest));

        await s.C.DeleteAsync($"/api/v1/approval-policies/{(string)policy["id"]!}");
    }

    [Fact]
    public async Task Bills_and_payments_accept_only_vendor_parties()
    {
        var s = await NewAsync();
        async Task<string> NewParty(string roles) => (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = "P " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles }), HttpStatusCode.Created))["id"]!;
        object BillFor(string party, string reference) => new { legalEntityId = s.Entity, partyId = party, vendorReference = reference, billDate = "2026-04-01", lines = new[] { new { description = "x", quantity = 1m, price = 10m, accountId = s.Expense } } };
        object PaymentFor(string party) => new { legalEntityId = s.Entity, partyId = party, date = "2026-04-22", method = "bankTransfer", bankAccountId = s.Bank, amount = 10m };
        var customer = await NewParty("Customer");
        var both = await NewParty("Customer, Vendor");

        var badBill = await s.C.PostAsJsonAsync($"{Fin}/bills", BillFor(customer, "R-1"));
        Assert.Equal(HttpStatusCode.BadRequest, badBill.StatusCode);
        Assert.Contains("partyid", (await badBill.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments", PaymentFor(customer))).StatusCode);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", BillFor(both, "R-2")), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", PaymentFor(both)), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", PaymentFor(s.Vendor)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Ageing_and_statement_reproduce_a_past_as_of_date()
    {
        var s = await NewAsync();
        var paidLater = await PostedAsync(s, Bill(s, 50m, "H-1", "2026-01-01", null)); // 100
        var voidedLater = await PostedAsync(s, Bill(s, 25m, "H-2", "2026-01-02", null)); // 50
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-02-15", method = "bankTransfer", bankAccountId = s.Bank, amount = 100m, allocations = new[] { new { billId = paidLater, amount = 100m } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills/{voidedLater}/void", VoidReason)); // voided today, after both dates below
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-02-15", method = "cash", bankAccountId = s.Bank, amount = 30m }), HttpStatusCode.Created); // advance

        async Task<decimal> Total(string asOf)
        {
            var report = await Ok(await s.C.GetAsync($"{Fin}/reports/ap-ageing?asOf={asOf}&legalEntityId={s.Entity}"));
            return (decimal?)report["rows"]!.AsArray().SingleOrDefault(r => (string)r!["partyId"]! == s.Vendor)?["total"] ?? 0m;
        }
        Assert.Equal(150m, await Total("2026-01-31"));      // both bills open, the 15 Feb payments do not exist yet
        Assert.Equal(20m, await Total("2026-02-28"));        // first bill paid; second (voided only today) still open: 50 less the 30 advance
        var statement = await Ok(await s.C.GetAsync($"{Fin}/reports/vendor-statement?partyId={s.Vendor}&asOf=2026-01-31&legalEntityId={s.Entity}"));
        Assert.Equal((150m, 0m), ((decimal)statement["balance"]!, (decimal)statement["unallocatedPayments"]!));
        Assert.Equal(-30m, await Total("2026-12-31"));       // today: the second bill is voided, only the advance remains
    }

    [Fact]
    public async Task Allocation_and_cheque_clearing_requests_are_validated()
    {
        var s = await NewAsync();
        var billId = await PostedAsync(s, Bill(s, 100m, "C-1", "2026-02-05", null)); // 200
        var early = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-02-01", method = "bankTransfer", bankAccountId = s.Bank, amount = 200m }), HttpStatusCode.Created);
        var id = (string)early["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments/{id}/allocate", new { allocations = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments/{id}/allocate", new { allocations = new[] { new { billId, amount = 200m } } })).StatusCode); // bill dated after payment

        var cheque = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-05-01", method = "instrument", bankAccountId = s.Bank, amount = 10m, chequeNo = "900100" }), HttpStatusCode.Created);
        var chequeId = (string)cheque["paymentInstrumentId"]!;
        foreach (var date in new[] { "0001-01-01", "2026-04-30", "2099-01-01" })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date })).StatusCode);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/cheques/{chequeId}/clear-issued", new { date = "2026-05-02" }));
    }

    [Fact]
    public async Task A_failing_journal_leaves_a_payment_that_cannot_be_voided_untouched()
    {
        var s = await NewAsync();
        var billId = await PostedAsync(s, Bill(s, 50m, "L-1", tax: null)); // 100
        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = s.Vendor, date = "2026-04-20", method = "bankTransfer", bankAccountId = s.Bank, amount = 100m, allocations = new[] { new { billId, amount = 100m } } }), HttpStatusCode.Created);
        var id = (string)payment["id"]!;
        Assert.True((await s.C.PutAsJsonAsync($"/api/v1/legal-entities/{s.Entity}/books-lock", new { through = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") })).IsSuccessStatusCode);

        Assert.False((await s.C.PostAsJsonAsync($"{Fin}/payments/{id}/void", VoidReason)).IsSuccessStatusCode); // the reversal cannot post
        Assert.Equal("issued", (string)(await Ok(await s.C.GetAsync($"{Fin}/payments/{id}")))["status"]!);
        var bill = await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal(("paid", 0m), ((string)bill["status"]!, (decimal)bill["outstanding"]!));
    }
}
