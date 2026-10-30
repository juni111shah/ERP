using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Bank statement import, matching, booking of unmatched lines, transfers and the cash position.</summary>
public class BankTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Bank, string Cash, string Capital, string Charges);

    private async Task<Setup> NewAsync()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Bank " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Id(string code) => (string)accounts.First(a => (string?)a!["code"] == code)!["id"]!;
        return new Setup(c, entity, Id("1010"), Id("1000"), Id("3000"), Id("6300"));
    }

    private static async Task PostJournalAsync(Setup s, string date, string debit, string credit, decimal amount)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date, lines = new[] { new { accountId = debit, debit = amount, credit = 0m }, new { accountId = credit, debit = 0m, credit = amount } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsync($"{Fin}/journals/{(string)draft["id"]!}/post", null));
    }

    [Fact]
    public async Task Statement_lines_import_once_and_auto_match_ledger_entries()
    {
        var s = await NewAsync();
        await PostJournalAsync(s, "2026-05-02", s.Bank, s.Capital, 10_000m);   // money in
        await PostJournalAsync(s, "2026-05-10", s.Capital, s.Bank, 2_500m);    // money out
        var lines = new[]
        {
            new { date = "2026-05-03", description = "Deposit", reference = "D1", amount = 10_000m },
            new { date = "2026-05-11", description = "Withdrawal", reference = "W1", amount = -2_500m },
            new { date = "2026-05-15", description = "Account fee", reference = (string?)null, amount = -35m },
        };
        var first = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new { legalEntityId = s.Entity, lines }));
        Assert.Equal((3, 0), ((int)first["imported"]!, (int)first["duplicates"]!));
        var again = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new { legalEntityId = s.Entity, lines }));
        Assert.Equal((0, 3), ((int)again["imported"]!, (int)again["duplicates"]!));

        var matched = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/auto-match", new { legalEntityId = s.Entity }));
        Assert.Equal(2, (int)matched["matched"]!);

        var rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-05-31&legalEntityId={s.Entity}"));
        Assert.Equal(7_500m, (decimal)rec["ledgerBalance"]!);
        Assert.Single(rec["unmatchedStatement"]!.AsArray());
        Assert.Empty(rec["unmatchedLedger"]!.AsArray());
        Assert.False((bool)rec["reconciled"]!);
        Assert.Equal(-35m, (decimal)rec["unmatchedStatementTotal"]!);
    }

    [Fact]
    public async Task An_unmatched_line_is_booked_to_an_expense_and_the_bank_reconciles()
    {
        var s = await NewAsync();
        await PostJournalAsync(s, "2026-05-02", s.Bank, s.Capital, 1_000m);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new
        {
            legalEntityId = s.Entity,
            lines = new[] { new { date = "2026-05-02", description = "Deposit", reference = (string?)null, amount = 1_000m }, new { date = "2026-05-20", description = "Monthly fee", reference = (string?)null, amount = -50m } },
        }));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/auto-match", new { legalEntityId = s.Entity }));

        var rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-05-31&legalEntityId={s.Entity}"));
        var lineId = (string)rec["unmatchedStatement"]![0]!["id"]!;
        var booked = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/statement-lines/{lineId}/book", new { counterAccountId = s.Charges, memo = "Bank charges May" }));
        Assert.Equal("matched", (string)booked["status"]!);

        rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-05-31&legalEntityId={s.Entity}"));
        Assert.True((bool)rec["reconciled"]!);
        Assert.Equal(950m, (decimal)rec["ledgerBalance"]!);

        // unmatch puts the line back on the list (the ledger entry stays, so both sides show a difference)
        await Ok(await s.C.PostAsync($"{Fin}/bank/statement-lines/{lineId}/unmatch", null));
        rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-05-31&legalEntityId={s.Entity}"));
        Assert.Single(rec["unmatchedStatement"]!.AsArray());
        Assert.Single(rec["unmatchedLedger"]!.AsArray());
        // manual match fixes it, but only with the same amount
        var ledgerId = (string)rec["unmatchedLedger"]![0]!["journalLineId"]!;
        var ok = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/statement-lines/{lineId}/match", new { journalLineId = ledgerId }));
        Assert.Equal("matched", (string)ok["status"]!);
    }

    [Fact]
    public async Task A_manual_match_rejects_different_amounts()
    {
        var s = await NewAsync();
        await PostJournalAsync(s, "2026-06-01", s.Bank, s.Capital, 400m);
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new { legalEntityId = s.Entity, lines = new[] { new { date = "2026-06-01", description = "Deposit", reference = (string?)null, amount = 300m } } }));
        var rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-06-30&legalEntityId={s.Entity}"));
        var res = await s.C.PostAsJsonAsync($"{Fin}/bank/statement-lines/{(string)rec["unmatchedStatement"]![0]!["id"]!}/match", new { journalLineId = (string)rec["unmatchedLedger"]![0]!["journalLineId"]! });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Transfers_move_money_between_cash_and_bank_and_show_in_the_cash_position()
    {
        var s = await NewAsync();
        await PostJournalAsync(s, "2026-07-01", s.Cash, s.Capital, 5_000m);
        Assert.Equal(HttpStatusCode.Created, (await s.C.PostAsJsonAsync($"{Fin}/bank/transfers", new { legalEntityId = s.Entity, fromAccountId = s.Cash, toAccountId = s.Bank, amount = 3_000m, date = "2026-07-02", memo = "Bank deposit of cash" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bank/transfers", new { legalEntityId = s.Entity, fromAccountId = s.Cash, toAccountId = s.Cash, amount = 1m, date = "2026-07-02" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bank/transfers", new { legalEntityId = s.Entity, fromAccountId = s.Cash, toAccountId = s.Capital, amount = 1m, date = "2026-07-02" })).StatusCode); // not a cash account

        var pos = await Ok(await s.C.GetAsync($"{Fin}/bank/position?asOf=2026-07-31&legalEntityId={s.Entity}"));
        Assert.Equal(5_000m, (decimal)pos["total"]!);
        Assert.Equal(2_000m, (decimal)pos["accounts"]!.AsArray().Single(a => (string?)a!["code"] == "1000")!["balance"]!);
        Assert.Equal(3_000m, (decimal)pos["accounts"]!.AsArray().Single(a => (string?)a!["code"] == "1010")!["balance"]!);
    }

    [Fact]
    public async Task Statement_lines_and_transfers_need_a_real_date_and_a_booked_line_cannot_be_booked_twice()
    {
        var s = await NewAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new
        {
            legalEntityId = s.Entity, lines = new[] { new { date = "0001-01-01", description = "Fee", reference = (string?)null, amount = -5m } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/bank/transfers", new { legalEntityId = s.Entity, fromAccountId = s.Cash, toAccountId = s.Bank, amount = 5m, date = "0001-01-01" })).StatusCode);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/accounts/{s.Bank}/import", new
        {
            legalEntityId = s.Entity, lines = new[] { new { date = "2026-05-20", description = "Fee", reference = (string?)null, amount = -50m } },
        }));
        var rec = await Ok(await s.C.GetAsync($"{Fin}/bank/accounts/{s.Bank}/reconciliation?asOf=2026-05-31&legalEntityId={s.Entity}"));
        var lineId = (string)rec["unmatchedStatement"]![0]!["id"]!;
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/bank/statement-lines/{lineId}/book", new { counterAccountId = s.Charges }));
        await Ok(await s.C.PostAsync($"{Fin}/bank/statement-lines/{lineId}/unmatch", null));
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/bank/statement-lines/{lineId}/book", new { counterAccountId = s.Charges })).StatusCode);
    }
}
