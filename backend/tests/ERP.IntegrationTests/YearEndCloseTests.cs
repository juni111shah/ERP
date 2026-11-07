using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Year-end close: the ledger effect, idempotency, the books lock, reopening, and what the reports show afterwards. Each test has its own legal entity.</summary>
public class YearEndCloseTests(ApiFactory f) : IClassFixture<ApiFactory>
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
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Close " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
        return new Setup(c, entity, accounts);
    }

    private static async Task<string> PostAsync(Setup s, string date, string debit, string credit, decimal amount)
    {
        var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date, lines = new[] { new { accountId = s[debit], debit = amount, credit = 0m }, new { accountId = s[credit], debit = 0m, credit = amount } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsync($"{Fin}/journals/{(string)draft["id"]!}/post", null));
        return (string)draft["id"]!;
    }

    private static Task<HttpResponseMessage> Close(Setup s, int year) => s.C.PostAsJsonAsync($"{Fin}/year-end/close", new { legalEntityId = s.Entity, fiscalYear = year });
    private static Task<HttpResponseMessage> Reopen(Setup s, int year, string? reason = "Late adjustment") => s.C.PostAsJsonAsync($"{Fin}/year-end/reopen", new { legalEntityId = s.Entity, fiscalYear = year, reason });

    private static async Task<decimal> ClosingAsync(Setup s, string code, string to)
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to={to}&legalEntityId={s.Entity}"));
        return tb["rows"]!.AsArray().Where(r => (string)r!["code"]! == code).Sum(r => (decimal)r!["closing"]!);
    }

    private static async Task<string?> BooksLockedToAsync(Setup s)
        => (string?)(await Ok(await s.C.GetAsync("/api/v1/legal-entities?pageSize=200"))).AsArray().Single(e => (string)e!["id"]! == s.Entity)!["booksLockedTo"];

    private static async Task<JsonNode> SheetAsync(Setup s, string asOf) => await Ok(await s.C.GetAsync($"{Fin}/reports/balance-sheet?asOf={asOf}&legalEntityId={s.Entity}"));

    private static decimal EquityLine(JsonNode sheet, string code) => sheet["equity"]!.AsArray().Where(l => (string)l!["code"]! == code).Sum(l => (decimal)l!["amount"]!);

    private async Task<Setup> WithActivityAsync()
    {
        var s = await NewAsync();
        await PostAsync(s, "2024-02-01", "1010", "3000", 10_000m); // capital
        await PostAsync(s, "2024-06-01", "1010", "4000", 1_500m);  // income
        await PostAsync(s, "2024-07-01", "6900", "1010", 400m);    // expense
        return s;
    }

    [Fact]
    public async Task Close_moves_income_and_expense_to_retained_earnings_in_one_journal_on_the_last_day_and_locks_the_books()
    {
        var s = await WithActivityAsync();
        var close = await Ok(await Close(s, 2024));
        Assert.Equal(1_100m, (decimal)close["netResult"]!);
        Assert.Equal("2024-01-01", (string)close["yearStart"]!);
        Assert.Equal("2024-12-31", (string)close["yearEnd"]!);
        Assert.False((bool)close["alreadyClosed"]!);

        var journal = await Ok(await s.C.GetAsync($"{Fin}/journals/{(string)close["journalEntryId"]!}"));
        Assert.Equal("2024-12-31", (string)journal["date"]!);
        Assert.Equal("yearend", (string)journal["sourceType"]!);
        var lines = journal["lines"]!.AsArray().ToDictionary(l => (string)l!["accountCode"]!, l => ((decimal)l!["debit"]!, (decimal)l!["credit"]!));
        Assert.Equal((1_500m, 0m), lines["4000"]);
        Assert.Equal((0m, 400m), lines["6900"]);
        Assert.Equal((0m, 1_100m), lines["3100"]);

        Assert.Equal(0m, await ClosingAsync(s, "4000", "2024-12-31"));
        Assert.Equal(0m, await ClosingAsync(s, "6900", "2024-12-31"));
        Assert.Equal(-1_100m, await ClosingAsync(s, "3100", "2024-12-31"));

        var locked = await BooksLockedToAsync(s);
        Assert.Equal("2024-12-31", locked);
        var late = await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date = "2024-12-30", lines = new[] { new { accountId = s["1010"], debit = 5m, credit = 0m }, new { accountId = s["4000"], debit = 0m, credit = 5m } } }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/journals/{(string)late["id"]!}/post", null)).StatusCode);
    }

    [Fact]
    public async Task Closing_twice_returns_the_same_close_and_posts_nothing_more()
    {
        var s = await WithActivityAsync();
        var first = await Ok(await Close(s, 2024));
        var again = await Ok(await Close(s, 2024));
        Assert.Equal((string)first["id"]!, (string)again["id"]!);
        Assert.Equal((string)first["journalEntryId"]!, (string)again["journalEntryId"]!);
        Assert.True((bool)again["alreadyClosed"]!);
        Assert.Equal(-1_100m, await ClosingAsync(s, "3100", "2024-12-31"));
        var closes = await Ok(await s.C.GetAsync($"{Fin}/year-end?filter=legalEntityId:eq:{s.Entity}"));
        Assert.Single(closes.AsArray());
    }

    [Fact]
    public async Task After_a_close_the_balance_sheet_shows_retained_earnings_and_only_the_open_year_in_current_earnings()
    {
        var s = await WithActivityAsync();
        await PostAsync(s, "2025-03-01", "1010", "4000", 700m);
        await Ok(await Close(s, 2024));

        var atYearEnd = await SheetAsync(s, "2024-12-31");
        Assert.Equal(1_100m, EquityLine(atYearEnd, "3100"));
        Assert.Equal(0m, (decimal)atYearEnd["currentEarnings"]!);
        Assert.True((bool)atYearEnd["balanced"]!);

        var nextYear = await SheetAsync(s, "2025-12-31");
        Assert.Equal(1_100m, EquityLine(nextYear, "3100"));
        Assert.Equal(700m, (decimal)nextYear["currentEarnings"]!);
        Assert.Equal(0m, (decimal)nextYear["priorOpenEarnings"]!);
        Assert.True((bool)nextYear["balanced"]!);

        // Before the close date the close is not there yet.
        var midYear = await SheetAsync(s, "2024-06-30");
        Assert.Equal(0m, EquityLine(midYear, "3100"));
        Assert.Equal(1_500m, (decimal)midYear["currentEarnings"]!);
        Assert.True((bool)midYear["balanced"]!);

        // A year after the close that was never closed itself: its result is shown apart from the open year, and the sheet still balances.
        await PostAsync(s, "2026-02-01", "1010", "4000", 50m);
        var later = await SheetAsync(s, "2026-06-30");
        Assert.Equal(50m, (decimal)later["currentEarnings"]!);
        Assert.Equal(700m, (decimal)later["priorOpenEarnings"]!);
        Assert.True((bool)later["balanced"]!);
    }

    [Fact]
    public async Task A_legal_entity_that_never_closed_keeps_cumulative_current_earnings()
    {
        var s = await WithActivityAsync();
        await PostAsync(s, "2025-03-01", "1010", "4000", 700m);
        var sheet = await SheetAsync(s, "2025-12-31");
        Assert.Equal(1_800m, (decimal)sheet["currentEarnings"]!);
        Assert.Equal(0m, (decimal)sheet["priorOpenEarnings"]!);
        Assert.Equal(0m, EquityLine(sheet, "3100"));
        Assert.True((bool)sheet["balanced"]!);
    }

    [Fact]
    public async Task The_income_statement_and_the_first_close_of_several_open_years_ignore_the_closing_entry()
    {
        var s = await WithActivityAsync();
        await PostAsync(s, "2025-03-01", "1010", "4000", 700m);
        var close = await Ok(await Close(s, 2025)); // sweeps 2024 and 2025
        Assert.Equal(1_800m, (decimal)close["netResult"]!);
        var year = await Ok(await s.C.GetAsync($"{Fin}/reports/income-statement?from=2025-01-01&to=2025-12-31&legalEntityId={s.Entity}"));
        Assert.Equal((700m, 0m, 700m), ((decimal)year["totalIncome"]!, (decimal)year["totalExpenses"]!, (decimal)year["netProfit"]!));
        // A later year is closed, so an earlier one cannot be closed after it.
        Assert.Equal(HttpStatusCode.Conflict, (await Close(s, 2024)).StatusCode);
    }

    [Fact]
    public async Task A_year_that_has_not_ended_cannot_be_closed_and_a_quiet_year_closes_without_a_journal()
    {
        var s = await NewAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Close(s, 2099)).StatusCode);
        var quiet = await Ok(await Close(s, 2023));
        Assert.Null(quiet["journalEntryId"]);
        Assert.Equal(0m, (decimal)quiet["netResult"]!);
    }

    [Fact]
    public async Task Reopen_reverses_the_closing_entry_on_the_same_date_and_restores_the_earlier_lock()
    {
        var s = await WithActivityAsync();
        await Ok(await s.C.PutAsJsonAsync($"/api/v1/legal-entities/{s.Entity}/books-lock", new { through = "2024-03-31" }), HttpStatusCode.NoContent);
        var close = await Ok(await Close(s, 2024));

        Assert.Equal(HttpStatusCode.BadRequest, (await Reopen(s, 2024, " ")).StatusCode); // a reason is required
        var reopened = await Ok(await Reopen(s, 2024));
        Assert.NotNull(reopened["reopenedAt"]);
        var reversal = await Ok(await s.C.GetAsync($"{Fin}/journals/{(string)reopened["reversalJournalId"]!}"));
        Assert.Equal("2024-12-31", (string)reversal["date"]!);
        Assert.Equal((string)close["journalEntryId"]!, (string)reversal["reversalOfId"]!);

        Assert.Equal(-1_500m, await ClosingAsync(s, "4000", "2024-12-31"));
        Assert.Equal(400m, await ClosingAsync(s, "6900", "2024-12-31"));
        Assert.Equal(0m, await ClosingAsync(s, "3100", "2024-12-31"));
        Assert.Equal("2024-03-31", await BooksLockedToAsync(s));

        // The income statement is still the year's real result, and the sheet goes back to current earnings.
        var year = await Ok(await s.C.GetAsync($"{Fin}/reports/income-statement?from=2024-01-01&to=2024-12-31&legalEntityId={s.Entity}"));
        Assert.Equal(1_100m, (decimal)year["netProfit"]!);
        Assert.Equal(1_100m, (decimal)(await SheetAsync(s, "2024-12-31"))["currentEarnings"]!);

        // The year is open: a correction posts, and closing again picks it up.
        await PostAsync(s, "2024-11-01", "1010", "4000", 100m);
        var second = await Ok(await Close(s, 2024));
        Assert.NotEqual((string)close["id"]!, (string)second["id"]!);
        Assert.Equal(1_200m, (decimal)second["netResult"]!);
        Assert.Equal(-1_200m, await ClosingAsync(s, "3100", "2024-12-31"));
    }

    [Fact]
    public async Task Only_the_latest_closed_year_can_be_reopened_and_nothing_can_undo_a_close_around_the_reopen_flow()
    {
        var s = await WithActivityAsync();
        var close = await Ok(await Close(s, 2024));
        await Ok(await Close(s, 2025));
        Assert.Equal(HttpStatusCode.Conflict, (await Reopen(s, 2024)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Reopen(s, 2023)).StatusCode); // never closed

        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/journals/{(string)close["journalEntryId"]!}/reverse", new { memo = "Nope" })).StatusCode);
        foreach (var through in new[] { "2024-12-31", null })
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PutAsJsonAsync($"/api/v1/legal-entities/{s.Entity}/books-lock", new { through })).StatusCode);
        Assert.Equal("2025-12-31", await BooksLockedToAsync(s));
    }

    [Fact]
    public async Task Closing_needs_its_permission_and_reopening_needs_a_separate_one()
    {
        var s = await WithActivityAsync();
        var roles = await Ok(await s.C.GetAsync("/api/v1/roles"));
        string Role(string name) => (string)roles.AsArray().First(r => (string?)r!["name"] == name)!["id"]!;
        async Task<HttpClient> UserAsync(string role)
        {
            var email = $"{role[..3].ToLowerInvariant()}{Guid.NewGuid():N}"[..20] + "@demo.local";
            (await s.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = role, temporaryPassword = "Temp#Passw0rd", roleIds = new[] { Role(role) } })).EnsureSuccessStatusCode();
            return (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
        }
        var accountant = await UserAsync("Accountant");
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync($"{Fin}/year-end/close", new { legalEntityId = s.Entity, fiscalYear = 2024 })).StatusCode);

        var manager = await UserAsync("Finance manager");
        await Ok(await manager.PostAsJsonAsync($"{Fin}/year-end/close", new { legalEntityId = s.Entity, fiscalYear = 2024 }));
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"{Fin}/year-end/reopen", new { legalEntityId = s.Entity, fiscalYear = 2024, reason = "Late bill" })).StatusCode);
        await Ok(await Reopen(s, 2024)); // the admin holds every permission
    }

    [Fact]
    public async Task The_fiscal_year_follows_the_start_month_setting()
    {
        var s = await WithActivityAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await s.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["core.fiscalYearStartMonth"] = 4 })).StatusCode);
        try
        {
            var close = await Ok(await Close(s, 2025)); // 1 Apr 2024 to 31 Mar 2025
            Assert.Equal("2024-04-01", (string)close["yearStart"]!);
            Assert.Equal("2025-03-31", (string)close["yearEnd"]!);
            Assert.Equal(1_100m, (decimal)close["netResult"]!);
        }
        finally { await s.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["core.fiscalYearStartMonth"] = 1 }); }
    }
}
