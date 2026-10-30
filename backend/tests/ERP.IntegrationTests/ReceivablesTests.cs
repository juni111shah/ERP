using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Invoices, credit notes, receipts and the ageing report. Each test uses its own legal entity and customer.</summary>
public class ReceivablesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly object VoidReason = new { reason = "Entered in error" };
    private const string Fin = "/api/v1/finance";

    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Party, string Bank, string TaxId);

    private async Task<Setup> NewAsync()
    {
        var c = await AdminAsync();
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "AR " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var party = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var tax = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "V" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5", kind = "vat", rate = 5 }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, party, bank, tax);
    }

    private static object Invoice(Setup s, decimal price, string date = "2026-06-01", bool credit = false, bool tax = true, string? due = null)
        => new { legalEntityId = s.Entity, partyId = s.Party, invoiceDate = date, dueDate = due, isCreditNote = credit, lines = new[] { new { description = "Service", quantity = 2m, price, taxCodeId = tax ? s.TaxId : null } } };

    private static async Task<string> IssuedAsync(Setup s, object body)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", body), HttpStatusCode.Created);
        var issued = await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));
        return (string)issued["id"]!;
    }

    [Fact]
    public async Task An_invoice_computes_tax_is_numbered_on_issue_and_posts_balanced_entries()
    {
        var s = await NewAsync();
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, 500m)), HttpStatusCode.Created);
        Assert.StartsWith("DRAFT-", (string)draft["number"]!);
        Assert.Equal((1000m, 50m, 1050m), ((decimal)draft["net"]!, (decimal)draft["tax"]!, (decimal)draft["total"]!));
        Assert.Equal("2026-07-01", (string)draft["dueDate"]!); // 30 day customer terms

        var id = (string)draft["id"]!;
        var edited = await Ok(await s.C.PutAsJsonAsync($"{Fin}/invoices/{id}", Invoice(s, 1000m)));
        Assert.Equal(2100m, (decimal)edited["total"]!);

        var issued = await Ok(await s.C.PostAsync($"{Fin}/invoices/{id}/issue", null));
        Assert.StartsWith("INV-", (string)issued["number"]!);
        Assert.Equal("issued", (string)issued["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PutAsJsonAsync($"{Fin}/invoices/{id}", Invoice(s, 1m))).StatusCode);

        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={s.Entity}"));
        Assert.Equal(2100m, (decimal)tb["totalDebit"]!);
        Assert.Equal(2100m, (decimal)tb["totalCredit"]!);
        Assert.Equal(2100m, (decimal)tb["rows"]!.AsArray().Single(r => (string?)r!["code"] == "1100")!["closing"]!); // receivables
        Assert.Equal(100m, -(decimal)tb["rows"]!.AsArray().Single(r => (string?)r!["code"] == "2100")!["closing"]!); // output tax
    }

    [Fact]
    public async Task Receipts_settle_invoices_partly_then_fully_and_cannot_overpay()
    {
        var s = await NewAsync();
        var invoiceId = await IssuedAsync(s, Invoice(s, 500m, tax: false)); // total 1000

        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Party, date = "2026-06-10", method = "bankTransfer", bankAccountId = s.Bank, amount = 400m,
            allocations = new[] { new { invoiceId, amount = 400m } },
        }), HttpStatusCode.Created);
        Assert.StartsWith("RCT-", (string)receipt["number"]!);
        Assert.Equal(0m, (decimal)receipt["unallocated"]!);
        var inv = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal(("partlyPaid", 400m, 600m), ((string)inv["status"]!, (decimal)inv["paid"]!, (decimal)inv["outstanding"]!));

        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Party, date = "2026-06-11", method = "cash", bankAccountId = s.Bank, amount = 700m, allocations = new[] { new { invoiceId, amount = 700m } },
        })).StatusCode);

        // pay the rest with a bigger receipt: the excess stays as customer credit
        var big = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Party, date = "2026-06-12", method = "cash", bankAccountId = s.Bank, amount = 800m }), HttpStatusCode.Created);
        Assert.Equal(800m, (decimal)big["unallocated"]!);
        var allocated = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts/{(string)big["id"]!}/allocate", new { allocations = new[] { new { invoiceId, amount = 600m } } }));
        Assert.Equal(200m, (decimal)allocated["unallocated"]!);
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/void", VoidReason)).StatusCode); // paid: credit it instead
    }

    [Fact]
    public async Task Voiding_a_receipt_reopens_the_invoice_and_voiding_an_unpaid_invoice_reverses_the_ledger()
    {
        var s = await NewAsync();
        var invoiceId = await IssuedAsync(s, Invoice(s, 100m, tax: false)); // 200
        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Party, date = "2026-06-10", method = "card", bankAccountId = s.Bank, amount = 200m, allocations = new[] { new { invoiceId, amount = 200m } },
        }), HttpStatusCode.Created);
        Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}")))["status"]!);

        var voided = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts/{(string)receipt["id"]!}/void", VoidReason));
        Assert.Equal("voided", (string)voided["status"]!);
        var inv = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal(("issued", 0m), ((string)inv["status"]!, (decimal)inv["paid"]!));

        // The void reason is required (3-500 characters).
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsync($"{Fin}/invoices/{invoiceId}/void", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/void", new { reason = " x " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/void", new { reason = new string('x', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/void", VoidReason)).StatusCode);
        // Void audit columns: reason and time come back on the detail DTOs.
        var voidedInvoice = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal("Entered in error", (string?)voidedInvoice["voidReason"]);
        Assert.NotNull(voidedInvoice["voidedAt"]);
        Assert.Equal("Entered in error", (string?)voided["voidReason"]);
        Assert.NotNull(voided["voidedAt"]);
        Assert.Null(inv["voidReason"]);
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={s.Entity}"));
        Assert.All(tb["rows"]!.AsArray(), r => Assert.Equal(0m, (decimal)r!["closing"]!));
    }

    [Fact]
    public async Task A_credit_note_reduces_an_invoice_and_cannot_be_used_twice()
    {
        var s = await NewAsync();
        var invoiceId = await IssuedAsync(s, Invoice(s, 500m, tax: false));           // 1000
        var creditId = await IssuedAsync(s, Invoice(s, 150m, credit: true, tax: false)); // 300
        Assert.StartsWith("CN-", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{creditId}")))["number"]!);

        var applied = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId, amount = 250m }));
        Assert.Equal(("partlyPaid", 750m), ((string)applied["status"]!, (decimal)applied["outstanding"]!));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId, amount = 100m })).StatusCode); // only 50 left
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/apply-credit", new { invoiceId, amount = 10m })).StatusCode); // not a credit note
    }

    [Fact]
    public async Task A_credit_note_from_a_paid_invoice_becomes_on_account_credit_for_other_open_invoices()
    {
        var s = await NewAsync();
        var paidId = await IssuedAsync(s, Invoice(s, 100m, tax: false)); // 200
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new
        {
            legalEntityId = s.Entity, partyId = s.Party, date = "2026-06-10", method = "cash", bankAccountId = s.Bank, amount = 200m, allocations = new[] { new { invoiceId = paidId, amount = 200m } },
        }), HttpStatusCode.Created);
        var openId = await IssuedAsync(s, Invoice(s, 500m, tax: false)); // 1000, still open

        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{paidId}/credit-note", new { date = "2026-06-15" }), HttpStatusCode.Created);
        Assert.Equal(paidId, (string?)draft["creditNoteForId"]);
        var creditId = (string)(await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)))["id"]!;

        // The original is paid, so the note can't go back to it but can be used on another open invoice of the customer.
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId = paidId, amount = 50m })).StatusCode);
        var applied = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId = openId, amount = 200m }));
        Assert.Equal(("partlyPaid", 800m), ((string)applied["status"]!, (decimal)applied["outstanding"]!));

        // The on-account application is recorded as a CreditApplication row (note -> other invoice).
        var rows = await CreditApplicationsAsync(Guid.Parse(creditId));
        var row = Assert.Single(rows);
        Assert.Equal((Guid.Parse(openId), 200m), (row.InvoiceId, row.Amount));
    }

    private async Task<List<ERP.Domain.Finance.CreditApplication>> CreditApplicationsAsync(Guid creditNoteId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.CreditApplications.IgnoreQueryFilters().Where(a => a.CreditNoteId == creditNoteId).ToListAsync();
    }

    [Fact]
    public async Task A_credit_note_from_an_open_invoice_applies_only_to_that_invoice()
    {
        var s = await NewAsync();
        var sourceId = await IssuedAsync(s, Invoice(s, 100m, tax: false)); // 200
        var otherId = await IssuedAsync(s, Invoice(s, 100m, tax: false));  // 200
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{sourceId}/credit-note", new { date = "2026-06-15" }), HttpStatusCode.Created);
        var creditId = (string)(await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)))["id"]!;

        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId = otherId, amount = 50m })).StatusCode);
        var applied = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{creditId}/apply-credit", new { invoiceId = sourceId, amount = 200m }));
        Assert.Equal("paid", (string)applied["status"]!);
        Assert.Equal(sourceId, (string?)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{creditId}")))["creditNoteForId"]);
        var row = Assert.Single(await CreditApplicationsAsync(Guid.Parse(creditId)));
        Assert.Equal((Guid.Parse(sourceId), 200m), (row.InvoiceId, row.Amount));
    }

    [Fact]
    public async Task Ageing_buckets_follow_the_due_date_and_net_off_credit_and_unallocated_receipts()
    {
        var s = await NewAsync();
        await IssuedAsync(s, Invoice(s, 100m, "2026-01-01", tax: false, due: "2026-02-01"));  // 200, ~100+ days late on 2026-06-01
        await IssuedAsync(s, Invoice(s, 50m, "2026-05-01", tax: false, due: "2026-05-20"));   // 100, 12 days late
        await IssuedAsync(s, Invoice(s, 25m, "2026-05-25", tax: false, due: "2026-06-20"));   // 50, current
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Party, date = "2026-05-30", method = "cash", bankAccountId = s.Bank, amount = 30m }), HttpStatusCode.Created);

        var report = await Ok(await s.C.GetAsync($"{Fin}/reports/ar-ageing?asOf=2026-06-01&legalEntityId={s.Entity}"));
        var row = report["rows"]!.AsArray().Single(r => (string)r!["partyId"]! == s.Party)!;
        Assert.Equal((200m, 100m, 20m), ((decimal)row["over90"]!, (decimal)row["days1To30"]!, (decimal)row["current"]!)); // the 30 on account nets against current
        Assert.Equal(320m, (decimal)row["total"]!); // 350 invoiced less 30 received on account

        var statement = await Ok(await s.C.GetAsync($"{Fin}/reports/customer-statement?partyId={s.Party}&asOf=2026-06-01&legalEntityId={s.Entity}"));
        Assert.Equal(3, statement["items"]!.AsArray().Count);
        Assert.Equal(320m, (decimal)statement["balance"]!);
    }

    [Fact]
    public async Task Invoice_lines_and_business_units_must_point_at_existing_records()
    {
        var s = await NewAsync();
        var unknownItem = new { legalEntityId = s.Entity, partyId = s.Party, invoiceDate = "2026-06-01", lines = new[] { new { description = "X", quantity = 1m, price = 10m, itemId = Guid.NewGuid() } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", unknownItem)).StatusCode);
        var unknownAccount = new { legalEntityId = s.Entity, partyId = s.Party, invoiceDate = "2026-06-01", lines = new[] { new { description = "X", quantity = 1m, price = 10m, accountId = Guid.NewGuid() } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", unknownAccount)).StatusCode);
    }

    [Fact]
    public async Task Invoices_validate_input_and_respect_locked_books()
    {
        var s = await NewAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = s.Entity, partyId = s.Party, invoiceDate = "2026-06-01", lines = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, 10m, due: "2026-05-01"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = s.Entity, partyId = Guid.NewGuid(), invoiceDate = "2026-06-01", lines = new[] { new { description = "x", quantity = 1m, price = 1m } } })).StatusCode);

        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", Invoice(s, 10m, "2026-01-10")), HttpStatusCode.Created);
        await s.C.PutAsJsonAsync($"/api/v1/legal-entities/{s.Entity}/books-lock", new { through = "2026-03-31" });
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"{Fin}/invoices/{(string)draft["id"]!}")).StatusCode);
    }

    [Fact]
    public async Task Ageing_and_statement_reproduce_a_past_as_of_date()
    {
        var s = await NewAsync();
        var paidLater = await IssuedAsync(s, Invoice(s, 50m, "2026-01-01", tax: false)); // 100
        var voidedLater = await IssuedAsync(s, Invoice(s, 25m, "2026-01-02", tax: false)); // 50
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Party, date = "2026-02-15", method = "bankTransfer", bankAccountId = s.Bank, amount = 100m, allocations = new[] { new { invoiceId = paidLater, amount = 100m } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{voidedLater}/void", VoidReason)); // voided today, after both dates below
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = s.Party, date = "2026-02-15", method = "cash", bankAccountId = s.Bank, amount = 30m }), HttpStatusCode.Created); // on account

        async Task<decimal> Total(string asOf)
        {
            var report = await Ok(await s.C.GetAsync($"{Fin}/reports/ar-ageing?asOf={asOf}&legalEntityId={s.Entity}"));
            return (decimal?)report["rows"]!.AsArray().SingleOrDefault(r => (string)r!["partyId"]! == s.Party)?["total"] ?? 0m;
        }
        Assert.Equal(150m, await Total("2026-01-31"));
        Assert.Equal(20m, await Total("2026-02-28")); // first paid; second (voided only today) open: 50 less the 30 on account
        var statement = await Ok(await s.C.GetAsync($"{Fin}/reports/customer-statement?partyId={s.Party}&asOf=2026-01-31&legalEntityId={s.Entity}"));
        Assert.Equal((150m, 0m), ((decimal)statement["balance"]!, (decimal)statement["unallocatedReceipts"]!));
        Assert.Equal(-30m, await Total("2026-12-31")); // today: the second invoice is voided, only the receipt on account remains
    }
}
