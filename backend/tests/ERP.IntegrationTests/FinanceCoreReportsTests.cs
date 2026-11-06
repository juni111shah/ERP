using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Cash flow statement, opening balances and voids after a period lock, each checked against the ledger.</summary>
public class FinanceCoreReportsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, Dictionary<string, string> Accounts)
    {
        public string this[string code] => Accounts[code];
    }

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Core " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
        return new Setup(c, entity, accounts);
    }

    private static async Task<string> PartyAsync(Setup s, string roles) => (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = roles + " " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles, paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> TaxAsync(Setup s) => (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "V" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5", kind = "vat", rate = 5 }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> PostJournalAsync(Setup s, string date, params (string Code, decimal Debit, decimal Credit)[] lines)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date, lines = lines.Select(l => new { accountId = s[l.Code], debit = l.Debit, credit = l.Credit }).ToArray() }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsync($"{Fin}/journals/{(string)draft["id"]!}/post", null));
        return (string)draft["id"]!;
    }

    private static async Task<decimal> ClosingAsync(Setup s, string code, string to = "2026-12-31")
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to={to}&legalEntityId={s.Entity}"));
        return tb["rows"]!.AsArray().Where(r => (string)r!["code"]! == code).Sum(r => (decimal)r!["closing"]!);
    }

    private static JsonNode Section(JsonNode statement, string category) => statement["sections"]!.AsArray().Single(x => (string)x!["category"]! == category)!;
    private static decimal Line(JsonNode section, string code) => section["lines"]!.AsArray().Where(l => (string)l!["code"]! == code).Sum(l => (decimal)l!["net"]!);

    // ---- Cash flow ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_cash_flow_statement_classifies_by_reporting_group_and_ties_to_the_ledger()
    {
        var s = await NewAsync();
        await PostJournalAsync(s, "2025-11-01", ("1010", 5_000m, 0m), ("3000", 0m, 5_000m));  // before the range: opening cash
        await PostJournalAsync(s, "2026-01-10", ("1010", 1_200m, 0m), ("4000", 0m, 1_200m));  // operating in
        await PostJournalAsync(s, "2026-01-11", ("6900", 300m, 0m), ("1010", 0m, 300m));      // operating out
        await PostJournalAsync(s, "2026-01-12", ("1500", 2_000m, 0m), ("1010", 0m, 2_000m));  // buying equipment: investing
        await PostJournalAsync(s, "2026-01-13", ("1010", 4_000m, 0m), ("2900", 0m, 4_000m));  // loan: financing
        await PostJournalAsync(s, "2026-01-14", ("1010", 500m, 0m), ("3000", 0m, 500m));      // capital: financing
        await PostJournalAsync(s, "2026-01-15", ("1000", 250m, 0m), ("1010", 0m, 250m));      // bank to cash: not a flow
        await PostJournalAsync(s, "2026-01-16", ("1100", 90m, 0m), ("4100", 0m, 90m));        // no cash at all
        await PostJournalAsync(s, "2026-02-01", ("1010", 777m, 0m), ("4000", 0m, 777m));      // after the range

        var r = await Ok(await s.C.GetAsync($"{Fin}/reports/cash-flow?from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}"));
        Assert.Equal(5_000m, (decimal)r["openingBalance"]!);
        Assert.Equal("AED", (string)r["currencyCode"]!);

        var (operating, investing, financing) = (Section(r, "operating"), Section(r, "investing"), Section(r, "financing"));
        Assert.Equal(900m, (decimal)operating["net"]!);
        Assert.Equal(1_200m, (decimal)operating["inflows"]!);
        Assert.Equal(300m, (decimal)operating["outflows"]!);
        Assert.Equal(1_200m, Line(operating, "4000"));
        Assert.Equal(-300m, Line(operating, "6900"));
        Assert.Equal(-2_000m, (decimal)investing["net"]!);
        Assert.Equal(-2_000m, Line(investing, "1500"));
        Assert.Equal(4_500m, (decimal)financing["net"]!);
        Assert.Equal(4_000m, Line(financing, "2900"));
        Assert.Equal(500m, Line(financing, "3000"));

        Assert.Equal(3_400m, (decimal)r["netChange"]!);
        Assert.Equal(8_400m, (decimal)r["closingBalance"]!);
        Assert.Equal(250m, (decimal)r["internalTransfers"]!);
        Assert.True((bool)r["tiesOut"]!);
        Assert.Equal(await ClosingAsync(s, "1010", "2026-01-31") + await ClosingAsync(s, "1000", "2026-01-31"), (decimal)r["ledgerClosingBalance"]!);
        Assert.Equal((decimal)r["closingBalance"]!, (decimal)r["ledgerClosingBalance"]!);
    }

    [Fact]
    public async Task A_split_entry_divides_its_cash_and_a_reversal_takes_it_back()
    {
        var s = await NewAsync();
        var split = await PostJournalAsync(s, "2026-03-01", ("1010", 1_000m, 0m), ("2900", 0m, 400m), ("4000", 0m, 600m));
        var r = await Ok(await s.C.GetAsync($"{Fin}/reports/cash-flow?from=2026-03-01&to=2026-03-31&legalEntityId={s.Entity}"));
        Assert.Equal(600m, (decimal)Section(r, "operating")["net"]!);
        Assert.Equal(400m, (decimal)Section(r, "financing")["net"]!);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals/{split}/reverse", new { date = "2026-03-15", memo = "Mistake" }));
        var after = await Ok(await s.C.GetAsync($"{Fin}/reports/cash-flow?from=2026-03-01&to=2026-03-31&legalEntityId={s.Entity}"));
        Assert.Equal(0m, (decimal)after["netChange"]!);
        Assert.True((bool)after["tiesOut"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.GetAsync($"{Fin}/reports/cash-flow?from=2026-03-31&to=2026-03-01&legalEntityId={s.Entity}")).StatusCode);
    }

    // ---- Opening balances ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_opening_entry_balances_against_opening_equity_and_ties_the_subledgers_to_the_control_accounts()
    {
        var s = await NewAsync();
        var (customer, vendor) = (await PartyAsync(s, "Customer"), await PartyAsync(s, "Vendor"));
        var body = new
        {
            legalEntityId = s.Entity, date = "2026-01-01", memo = "Go-live balances",
            lines = new object[]
            {
                new { accountId = s["1010"], debit = 10_000m, credit = 0m },
                new { accountId = s["1100"], debit = 3_000m, credit = 0m, partyId = customer, dueDate = "2026-02-15", reference = "OLD-1" },
                new { accountId = s["1100"], debit = 0m, credit = 500m, partyId = customer, reference = "OLD-CREDIT" },
                new { accountId = s["2000"], debit = 0m, credit = 2_000m, partyId = vendor, dueDate = "2026-03-01" },
                new { accountId = s["2900"], debit = 0m, credit = 1_000m },
            },
        };
        var posted = await Ok(await s.C.PostAsJsonAsync($"{Fin}/opening-balances", body), HttpStatusCode.Created);
        Assert.Equal(9_500m, (decimal)posted["openingEquityAmount"]!);
        Assert.Equal(2, posted["invoiceIds"]!.AsArray().Count);
        Assert.Single(posted["billIds"]!.AsArray());

        Assert.Equal(10_000m, await ClosingAsync(s, "1010"));
        Assert.Equal(2_500m, await ClosingAsync(s, "1100"));
        Assert.Equal(-2_000m, await ClosingAsync(s, "2000"));
        Assert.Equal(-1_000m, await ClosingAsync(s, "2900"));
        Assert.Equal(-9_500m, await ClosingAsync(s, "3900"));

        // Subledger = control account, in both reports built from documents.
        var arAgeing = await Ok(await s.C.GetAsync($"{Fin}/reports/ar-ageing?asOf=2026-12-31&legalEntityId={s.Entity}"));
        Assert.Equal(2_500m, (decimal)arAgeing["totals"]!["total"]!);
        var apAgeing = await Ok(await s.C.GetAsync($"{Fin}/reports/ap-ageing?asOf=2026-12-31&legalEntityId={s.Entity}"));
        Assert.Equal(2_000m, (decimal)apAgeing["totals"]!["total"]!);
        Assert.Equal(2_500m, (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/customer-ledger?partyId={customer}&from=2026-01-01&to=2026-12-31&legalEntityId={s.Entity}")))["closingBalance"]!);
        Assert.Equal(2_000m, (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/vendor-ledger?partyId={vendor}&from=2026-01-01&to=2026-12-31&legalEntityId={s.Entity}")))["closingBalance"]!);

        // Opening documents are no sales or purchases for the tax reports, and their cash is opening cash.
        var tax = await Ok(await s.C.GetAsync($"{Fin}/reports/tax-summary?from=2026-01-01&to=2026-12-31&legalEntityId={s.Entity}"));
        Assert.Empty(tax["rows"]!.AsArray());
        var flow = await Ok(await s.C.GetAsync($"{Fin}/reports/cash-flow?from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}"));
        Assert.Equal(10_000m, (decimal)flow["openingBalance"]!);
        Assert.Equal(0m, (decimal)flow["netChange"]!);
        Assert.True((bool)flow["tiesOut"]!);

        // A receipt settles the opening invoice the normal way, and the subledger still ties.
        var invoice = (string)posted["invoiceIds"]!.AsArray()[0]!;
        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = customer, date = "2026-02-01", method = "bankTransfer", bankAccountId = s["1010"], amount = 1_000m, allocations = new[] { new { invoiceId = invoice, amount = 1_000m } } }), HttpStatusCode.Created);
        Assert.Equal(1_500m, await ClosingAsync(s, "1100"));
        Assert.Equal(1_500m, (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/ar-ageing?asOf=2026-12-31&legalEntityId={s.Entity}")))["totals"]!["total"]!);
    }

    [Fact]
    public async Task An_opening_entry_runs_once_unless_reversed_and_its_documents_are_undone_only_through_it()
    {
        var s = await NewAsync();
        var customer = await PartyAsync(s, "Customer");
        object Body(decimal bank) => new
        {
            legalEntityId = s.Entity, date = "2026-01-01",
            lines = new object[] { new { accountId = s["1010"], debit = bank, credit = 0m }, new { accountId = s["1100"], debit = 700m, credit = 0m, partyId = customer } },
        };
        var first = await Ok(await s.C.PostAsJsonAsync($"{Fin}/opening-balances", Body(1_000m)), HttpStatusCode.Created);
        var journal = (string)first["journalId"]!;
        var invoice = (string)first["invoiceIds"]!.AsArray()[0]!;
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/opening-balances", Body(1_000m))).StatusCode);

        // Neither the journal nor the document can be undone on its own.
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/journals/{journal}/reverse", new { memo = "No" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Entered in error" })).StatusCode);

        // Settled documents block the reversal until the receipt is voided.
        var receipt = await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = customer, date = "2026-02-01", method = "bankTransfer", bankAccountId = s["1010"], amount = 700m, allocations = new[] { new { invoiceId = invoice, amount = 700m } } }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/opening-balances/{journal}/reverse", new { reason = "Wrong figures" })).StatusCode);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts/{(string)receipt["id"]!}/void", new { reason = "Wrong figures" }));

        var reversed = await Ok(await s.C.PostAsJsonAsync($"{Fin}/opening-balances/{journal}/reverse", new { reason = "Wrong figures" }));
        Assert.Equal("reversed", (string)reversed["status"]!);
        Assert.Equal(0m, await ClosingAsync(s, "1100"));
        Assert.Equal(0m, await ClosingAsync(s, "3900"));
        Assert.Equal("voided", (string)(await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoice}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/opening-balances/{journal}/reverse", new { reason = "Again now" })).StatusCode);

        var second = await Ok(await s.C.PostAsJsonAsync($"{Fin}/opening-balances", Body(1_250m)), HttpStatusCode.Created);
        Assert.Equal(1_950m, (decimal)second["openingEquityAmount"]!);
        Assert.Equal(-1_950m, await ClosingAsync(s, "3900"));
    }

    [Fact]
    public async Task Opening_lines_are_checked()
    {
        var s = await NewAsync();
        var (customer, vendor) = (await PartyAsync(s, "Customer"), await PartyAsync(s, "Vendor"));
        async Task Refused(params object[] lines)
            => Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/opening-balances", new { legalEntityId = s.Entity, date = "2026-01-01", lines })).StatusCode);
        await Refused(new { accountId = s["1100"], debit = 100m, credit = 0m }); // receivable without a party
        await Refused(new { accountId = s["2000"], debit = 0m, credit = 100m }); // payable without a party
        await Refused(new { accountId = s["1100"], debit = 100m, credit = 0m, partyId = vendor }); // a vendor is no customer
        await Refused(new { accountId = s["2000"], debit = 100m, credit = 0m, partyId = vendor }); // vendors open with a credit
        await Refused(new { accountId = s["4000"], debit = 0m, credit = 100m }); // income is not a balance sheet account
        await Refused(new { accountId = s["3900"], debit = 0m, credit = 100m }); // the offset is automatic
        await Refused(new { accountId = s["1010"], debit = 100m, credit = 100m });
        await Refused();
        Assert.Equal(0m, await ClosingAsync(s, "1010"));
    }

    // ---- Void after lock ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Voiding_after_the_lock_leaves_the_locked_period_reports_unchanged_and_shows_the_void_in_the_period_it_happened()
    {
        var s = await NewAsync();
        var (customer, vendor) = (await PartyAsync(s, "Customer"), await PartyAsync(s, "Vendor"));
        var taxCode = await TaxAsync(s);

        var invoiceDraft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = s.Entity, partyId = customer, invoiceDate = "2026-01-15", lines = new[] { new { description = "Service", quantity = 1m, price = 1_000m, taxCodeId = taxCode } } }), HttpStatusCode.Created);
        var invoice = (string)invoiceDraft["id"]!;
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{invoice}/issue", null));
        var billDraft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = s.Entity, partyId = vendor, vendorReference = "V-" + Guid.NewGuid().ToString("N")[..6], billDate = "2026-01-20", lines = new[] { new { description = "Supplies", quantity = 1m, price = 400m, accountId = s["6900"], taxCodeId = taxCode } } }), HttpStatusCode.Created);
        var bill = (string)billDraft["id"]!;
        await Ok(await s.C.PostAsync($"{Fin}/bills/{bill}/post", null));
        var receipt = (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = s.Entity, partyId = customer, date = "2026-01-25", method = "bankTransfer", bankAccountId = s["1010"], amount = 200m }), HttpStatusCode.Created))["id"]!;
        await Ok(await s.C.PutAsJsonAsync($"/api/v1/legal-entities/{s.Entity}/books-lock", new { through = "2026-01-31" }), HttpStatusCode.NoContent);

        async Task<(string Tax, string Tb, decimal Customer, decimal Vendor)> LockedPeriodAsync()
        {
            var tax = await s.C.GetStringAsync($"{Fin}/reports/tax-summary?from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}");
            var tb = await s.C.GetStringAsync($"{Fin}/reports/trial-balance?from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}");
            var cust = (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/customer-ledger?partyId={customer}&from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}")))["closingBalance"]!;
            var vend = (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/vendor-ledger?partyId={vendor}&from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}")))["closingBalance"]!;
            return (Strip(tax), Strip(tb), cust, vend);
        }
        var before = await LockedPeriodAsync();
        Assert.Equal(850m, before.Customer); // 1050 invoice - 200 receipt

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Entered in error" }));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills/{bill}/void", new { reason = "Entered in error" }));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/receipts/{receipt}/void", new { reason = "Entered in error" }));
        Assert.Equal(before, await LockedPeriodAsync());

        // The void shows up in the open period it happened in, and over the whole life everything nets to nothing.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var whole = await Ok(await s.C.GetAsync($"{Fin}/reports/tax-summary?from=2026-01-01&to={today:yyyy-MM-dd}&legalEntityId={s.Entity}"));
        Assert.Equal(0m, (decimal)whole["outputTax"]!);
        Assert.Equal(0m, (decimal)whole["inputTax"]!);
        var now = await Ok(await s.C.GetAsync($"{Fin}/reports/tax-summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&legalEntityId={s.Entity}"));
        Assert.Equal(-50m, (decimal)now["outputTax"]!);
        Assert.Equal(-20m, (decimal)now["inputTax"]!);
        var statement = await Ok(await s.C.GetAsync($"{Fin}/reports/customer-ledger?partyId={customer}&from=2026-01-01&to={today:yyyy-MM-dd}&legalEntityId={s.Entity}"));
        Assert.Equal(0m, (decimal)statement["closingBalance"]!);
        Assert.Contains(statement["lines"]!.AsArray(), l => (string)l!["kind"]! == "void");
        Assert.Equal(0m, await ClosingAsync(s, "1100", today.ToString("yyyy-MM-dd")));
    }

    [Fact]
    public async Task Voiding_in_an_open_period_still_takes_the_document_out_of_its_period()
    {
        var s = await NewAsync();
        var customer = await PartyAsync(s, "Customer");
        var taxCode = await TaxAsync(s);
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = s.Entity, partyId = customer, invoiceDate = "2026-01-15", lines = new[] { new { description = "Service", quantity = 1m, price = 1_000m, taxCodeId = taxCode } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{(string)draft["id"]!}/void", new { reason = "Entered in error" }));
        var tax = await Ok(await s.C.GetAsync($"{Fin}/reports/tax-summary?from=2026-01-01&to=2026-01-31&legalEntityId={s.Entity}"));
        Assert.Empty(tax["rows"]!.AsArray());
    }

    private static string Strip(string envelope) => JsonNode.Parse(envelope)!["data"]!.ToJsonString();
}

/// <summary>Reports across legal entities with different base currencies. A fixture of its own: the second currency has to stay out of the other tests' workspace.</summary>
public class MixedCurrencyReportTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Reports_refuse_a_scope_that_mixes_base_currencies_and_work_for_one_legal_entity()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
        async Task<string> EntityAsync(string currency) => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = currency + " " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = currency, isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        async Task PostAsync(string entity)
        {
            var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = entity, date = "2026-01-10", lines = new[] { new { accountId = accounts["1010"], debit = 100m, credit = 0m }, new { accountId = accounts["4000"], debit = 0m, credit = 100m } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/journals/{(string)draft["id"]!}/post", null));
        }
        var aed = await EntityAsync("AED");
        await PostAsync(aed);
        var qar = await EntityAsync("QAR");

        var scoped = new[]
        {
            $"{Fin}/reports/trial-balance?to=2026-12-31", $"{Fin}/reports/income-statement?from=2026-01-01&to=2026-12-31", $"{Fin}/reports/balance-sheet?asOf=2026-12-31",
            $"{Fin}/reports/general-ledger?accountId={accounts["1010"]}&to=2026-12-31", $"{Fin}/reports/cash-flow?from=2026-01-01&to=2026-12-31", $"{Fin}/reports/cash-movements?from=2026-01-01&to=2026-12-31",
            $"{Fin}/reports/tax-summary?from=2026-01-01&to=2026-12-31", $"{Fin}/reports/ar-ageing?asOf=2026-12-31", $"{Fin}/reports/ap-ageing?asOf=2026-12-31", $"{Fin}/reports/cash-forecast",
        };

        // A second currency nobody has posted in mixes nothing.
        await Ok(await c.GetAsync(scoped[0]));

        await PostAsync(qar);
        foreach (var url in scoped)
        {
            var refused = await c.GetAsync(url);
            Assert.True(refused.StatusCode == HttpStatusCode.BadRequest, $"{url} answered {refused.StatusCode}");
            var text = await refused.Content.ReadAsStringAsync();
            Assert.Contains("different base currencies", text);
            Assert.Contains("AED", text);
            Assert.Contains("QAR", text);
        }

        // One legal entity, by parameter or by the selected working entity, is fine.
        var one = await Ok(await c.GetAsync($"{scoped[0]}&legalEntityId={qar}"));
        Assert.Equal(100m, (decimal)one["totalDebit"]!);
        var request = new HttpRequestMessage(HttpMethod.Get, scoped[0]);
        request.Headers.Add("X-Entity-Id", aed);
        Assert.Equal(100m, (decimal)(await Ok(await c.SendAsync(request)))["totalDebit"]!);
    }
}
