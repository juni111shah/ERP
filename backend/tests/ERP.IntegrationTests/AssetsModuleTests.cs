using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The fixed assets module against the ledger: capitalise, depreciate, dispose, reports and the dashboard. Each test has its own legal entity.</summary>
public class AssetsModuleTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Assets = "/api/v1/m/assets";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, Dictionary<string, string> Accounts, string Category, string Declining)
    {
        public string this[string code] => Accounts[code];
    }

    private async Task<Setup> NewAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/assets/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Assets " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
        async Task<string> Category(string method, int life)
            => (string)(await Ok(await c.PostAsJsonAsync($"{Assets}/categories", new
            {
                code = "C" + Guid.NewGuid().ToString("N")[..6], name = "Equipment " + method, lifeMonths = life, method, assetAccountId = accounts["1500"], accumulatedDepreciationAccountId = accounts["1590"],
                depreciationExpenseAccountId = accounts["6400"], disposalGainLossAccountId = accounts["4900"],
            }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, accounts, await Category("straightLine", 10), await Category("decliningBalance", 10));
    }

    private static async Task<string> AssetAsync(Setup s, decimal cost = 1_000m, decimal salvage = 0m, string? category = null, string acquired = "2025-01-15", object? unit = null)
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Assets}/register", new { legalEntityId = s.Entity, name = "Machine", categoryId = category ?? s.Category, acquisitionDate = acquired, cost, salvageValue = salvage, custodian = "Ops team", businessUnitId = unit }), HttpStatusCode.Created))["id"]!;

    private static async Task<JsonNode> CapitaliseAsync(Setup s, string asset, string bank = "1010")
        => await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{asset}/capitalise", new { source = "bank", accountId = s[bank] }));

    private static Task<HttpResponseMessage> Run(Setup s, int year, int month) => s.C.PostAsJsonAsync($"{Assets}/depreciation-runs", new { legalEntityId = s.Entity, year, month });

    private static async Task<decimal> ClosingAsync(Setup s, string code, string to = "2026-10-31")
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to={to}&legalEntityId={s.Entity}"));
        return tb["rows"]!.AsArray().Where(r => (string)r!["code"]! == code).Sum(r => (decimal)r!["closing"]!);
    }

    [Fact]
    public async Task Capitalising_posts_the_cost_against_the_bank_or_a_clearing_account()
    {
        var s = await NewAsync();
        var asset = await AssetAsync(s);
        Assert.Equal(0m, await ClosingAsync(s, "1500")); // a draft in the register is not on the books

        var done = await CapitaliseAsync(s, asset);
        Assert.Equal("capitalised", (string)done["status"]!);
        Assert.Equal("2025-01-15", (string)done["capitalisedOn"]!);
        Assert.Equal(1_000m, await ClosingAsync(s, "1500"));
        Assert.Equal(-1_000m, await ClosingAsync(s, "1010"));
        var journal = await Ok(await s.C.GetAsync($"{Fin}/journals/{(string)done["capitalisationJournalId"]!}"));
        Assert.Equal("fixedasset", (string)journal["sourceType"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Assets}/register/{asset}/capitalise", new { source = "bank", accountId = s["1010"] })).StatusCode); // only once

        var second = await AssetAsync(s, 300m);
        await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{second}/capitalise", new { source = "clearing", accountId = s["2050"], date = "2025-02-01" }));
        Assert.Equal(1_300m, await ClosingAsync(s, "1500"));
        Assert.Equal(-300m, await ClosingAsync(s, "2050"));

        var third = await AssetAsync(s, 50m);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register/{third}/capitalise", new { source = "bank", accountId = s["6900"] })).StatusCode); // not a bank account
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register/{third}/capitalise", new { source = "clearing" })).StatusCode); // no account
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register/{third}/capitalise", new { source = "clearing", accountId = s["2050"], date = "2024-01-01" })).StatusCode); // before acquisition
    }

    [Fact]
    public async Task Buying_on_credit_raises_a_bill_so_the_payables_ledger_ties_and_the_bill_cannot_be_voided_under_the_asset()
    {
        var s = await NewAsync();
        var vendor = (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = "Supplier " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var asset = await AssetAsync(s, 2_000m);
        var done = await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{asset}/capitalise", new { source = "vendor", partyId = vendor, reference = "SUP-" + Guid.NewGuid().ToString("N")[..6] }));
        Assert.Equal(2_000m, await ClosingAsync(s, "1500"));
        Assert.Equal(-2_000m, await ClosingAsync(s, "2000"));
        Assert.Equal(2_000m, (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/ap-ageing?asOf=2026-10-31&legalEntityId={s.Entity}")))["totals"]!["total"]!);

        var billId = (string)done["sourceBillId"]!;
        var bill = await Ok(await s.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal("issued", (string)bill["status"]!);
        Assert.Equal((string)done["capitalisationJournalId"]!, (string)bill["journalEntryId"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/bills/{billId}/void", new { reason = "Entered in error" })).StatusCode);
        Assert.Equal(2_000m, await ClosingAsync(s, "1500"));

        // Paying that bill is an ordinary payment.
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = s.Entity, partyId = vendor, date = "2025-03-01", method = "bankTransfer", bankAccountId = s["1010"], amount = 2_000m, allocations = new[] { new { billId, amount = 2_000m } } }), HttpStatusCode.Created);
        Assert.Equal(0m, await ClosingAsync(s, "2000"));
    }

    [Fact]
    public async Task Linking_to_a_posted_bill_moves_an_expense_line_to_the_asset_or_adds_nothing_when_it_is_already_there()
    {
        var s = await NewAsync();
        var vendor = (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = "Supplier " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        async Task<JsonNode> PostedBillAsync(object[] lines)
        {
            var draft = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = s.Entity, partyId = vendor, billDate = "2025-01-10", lines }), HttpStatusCode.Created);
            return await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)draft["id"]!}/post", null));
        }

        var onExpense = await PostedBillAsync([new { description = "Printer", quantity = 1m, price = 600m, accountId = s["6900"] }]);
        Assert.Equal(600m, await ClosingAsync(s, "6900"));
        var a1 = await AssetAsync(s, 600m);
        var moved = await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{a1}/capitalise", new { source = "bill", billId = (string)onExpense["id"]! }));
        Assert.Equal((string)onExpense["id"]!, (string)moved["sourceBillId"]!);
        Assert.Equal(600m, await ClosingAsync(s, "1500"));
        Assert.Equal(0m, await ClosingAsync(s, "6900"));
        Assert.Equal(-600m, await ClosingAsync(s, "2000")); // the bill's credit stands

        var onAsset = await PostedBillAsync([new { description = "Press", quantity = 1m, price = 900m, accountId = s["1500"] }, new { description = "Delivery", quantity = 1m, price = 50m, accountId = s["6900"] }]);
        var a2 = await AssetAsync(s, 900m);
        var linked = await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{a2}/capitalise", new { source = "bill", billId = (string)onAsset["id"]! }));
        Assert.Equal((string)onAsset["journalEntryId"]!, (string)linked["capitalisationJournalId"]!); // no second entry
        Assert.Equal(1_500m, await ClosingAsync(s, "1500"));

        // A line can only be capitalised once, up to its amount, and the bill stays put.
        var a3 = await AssetAsync(s, 100m);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Assets}/register/{a3}/capitalise", new { source = "bill", billId = (string)onAsset["id"]! })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/bills/{(string)onAsset["id"]!}/void", new { reason = "Entered in error" })).StatusCode);

        var draftBill = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = s.Entity, partyId = vendor, billDate = "2025-01-10", lines = new[] { new { description = "Unposted", quantity = 1m, price = 100m, accountId = s["6900"] } } }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register/{a3}/capitalise", new { source = "bill", billId = (string)draftBill["id"]! })).StatusCode);
    }

    [Fact]
    public async Task A_depreciation_run_posts_one_journal_per_period_and_running_it_again_changes_nothing()
    {
        var s = await NewAsync();
        await CapitaliseAsync(s, await AssetAsync(s, 1_000m, 100m)); // 90 a month over 10 months
        await CapitaliseAsync(s, await AssetAsync(s, 500m));         // 50 a month

        var run = await Ok(await Run(s, 2025, 2));
        Assert.False((bool)run["alreadyRun"]!);
        Assert.Equal((140m, 2), ((decimal)run["total"]!, (int)run["assetCount"]!));
        var journal = await Ok(await s.C.GetAsync($"{Fin}/journals/{(string)run["journalEntryId"]!}"));
        Assert.Equal("2025-02-28", (string)journal["date"]!);
        var lines = journal["lines"]!.AsArray();
        Assert.Equal(2, lines.Count); // same accounts: one debit and one credit line
        Assert.Equal((140m, 0m), ((decimal)lines.Single(l => (string)l!["accountCode"]! == "6400")!["debit"]!, (decimal)lines.Single(l => (string)l!["accountCode"]! == "6400")!["credit"]!));
        Assert.Equal((0m, 140m), ((decimal)lines.Single(l => (string)l!["accountCode"]! == "1590")!["debit"]!, (decimal)lines.Single(l => (string)l!["accountCode"]! == "1590")!["credit"]!));

        var again = await Ok(await Run(s, 2025, 2));
        Assert.True((bool)again["alreadyRun"]!);
        Assert.Equal((string)run["id"]!, (string)again["id"]!);
        Assert.Equal(140m, await ClosingAsync(s, "6400"));
        Assert.Equal(-140m, await ClosingAsync(s, "1590"));
        Assert.Single((await Ok(await s.C.GetAsync($"{Assets}/depreciation-runs?filter=legalEntityId:eq:{s.Entity}"))).AsArray());

        // Not a month that is still running, and not backwards.
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1);
        Assert.Equal(HttpStatusCode.BadRequest, (await Run(s, future.Year, future.Month)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Run(s, 2025, 13)).StatusCode);
        await Ok(await Run(s, 2025, 4));
        Assert.Equal(HttpStatusCode.Conflict, (await Run(s, 2025, 3)).StatusCode);
        Assert.Equal(140m * 3, await ClosingAsync(s, "6400")); // the skipped March was caught up in April

        // The journal cannot be undone behind the module's back.
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/journals/{(string)run["journalEntryId"]!}/reverse", new { memo = "No" })).StatusCode);
    }

    [Fact]
    public async Task Depreciation_stops_at_the_salvage_value_under_both_methods()
    {
        var s = await NewAsync();
        var straight = await AssetAsync(s, 1_000m, 100m);
        var declining = await AssetAsync(s, 1_000m, 100m, s.Declining);
        await CapitaliseAsync(s, straight);
        await CapitaliseAsync(s, declining);

        await Ok(await Run(s, 2025, 2));
        var firstDeclining = await Ok(await s.C.GetAsync($"{Assets}/register/{declining}"));
        Assert.Equal(200m, (decimal)firstDeclining["accumulatedDepreciation"]!); // double declining: 2/10 of 1000

        await Ok(await Run(s, 2025, 3));
        Assert.Equal(360m, (decimal)(await Ok(await s.C.GetAsync($"{Assets}/register/{declining}")))["accumulatedDepreciation"]!); // 2/10 of the 800 left
        Assert.Equal(180m, (decimal)(await Ok(await s.C.GetAsync($"{Assets}/register/{straight}")))["accumulatedDepreciation"]!);

        // Well past the life of both.
        await Ok(await Run(s, 2026, 6));
        foreach (var id in new[] { straight, declining })
        {
            var a = await Ok(await s.C.GetAsync($"{Assets}/register/{id}"));
            Assert.Equal("fullyDepreciated", (string)a["status"]!);
            Assert.Equal(900m, (decimal)a["accumulatedDepreciation"]!);
            Assert.Equal(100m, (decimal)a["netBookValue"]!);
        }
        Assert.Equal(1_800m, await ClosingAsync(s, "6400"));

        var idle = await Ok(await Run(s, 2026, 7));
        Assert.Equal(0m, (decimal)idle["total"]!);
        Assert.Null(idle["journalEntryId"]);
        Assert.Equal(1_800m, await ClosingAsync(s, "6400"));
    }

    [Fact]
    public async Task Disposal_posts_the_gain_or_loss_and_takes_the_asset_off_the_books()
    {
        var s = await NewAsync();
        var gainAsset = await AssetAsync(s, 1_000m); // 100 a month
        var lossAsset = await AssetAsync(s, 1_000m);
        await CapitaliseAsync(s, gainAsset);
        await CapitaliseAsync(s, lossAsset);

        // Book value has to be up to date first.
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Assets}/register/{gainAsset}/dispose", new { date = "2025-05-10", proceeds = 0m })).StatusCode);
        await Ok(await Run(s, 2025, 4)); // Feb, Mar, Apr: accumulated 300 each, net book value 700
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Assets}/register/{gainAsset}/dispose", new { date = "2025-04-30", proceeds = 0m })).StatusCode); // already charged through then

        var gain = await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{gainAsset}/dispose", new { date = "2025-05-10", proceeds = 900m, proceedsAccountId = s["1010"], reason = "Sold" }));
        Assert.Equal("disposed", (string)gain["status"]!);
        Assert.Equal(200m, (decimal)gain["disposalGainLoss"]!);
        var loss = await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{lossAsset}/dispose", new { date = "2025-05-10", proceeds = 100m, proceedsAccountId = s["1010"] }));
        Assert.Equal(-600m, (decimal)loss["disposalGainLoss"]!);

        Assert.Equal(0m, await ClosingAsync(s, "1500"));
        Assert.Equal(0m, await ClosingAsync(s, "1590"));
        Assert.Equal(-2_000m + 1_000m, await ClosingAsync(s, "1010"));
        Assert.Equal(400m, await ClosingAsync(s, "4900")); // Dr 600 loss, Cr 200 gain, on the same account

        // A disposed asset depreciates no more and cannot be disposed twice.
        var next = await Ok(await Run(s, 2025, 6));
        Assert.Equal(0m, (decimal)next["total"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Assets}/register/{gainAsset}/dispose", new { date = "2025-07-01", proceeds = 0m })).StatusCode);

        // Without a gain or loss account there is nowhere to post the difference.
        var plain = (string)(await Ok(await s.C.PostAsJsonAsync($"{Assets}/categories", new { code = "N" + Guid.NewGuid().ToString("N")[..6], name = "No default", lifeMonths = 10, method = "straightLine", assetAccountId = s["1500"], accumulatedDepreciationAccountId = s["1590"], depreciationExpenseAccountId = s["6400"] }), HttpStatusCode.Created))["id"]!;
        var other = await AssetAsync(s, 100m, 0m, plain, "2025-08-15");
        await CapitaliseAsync(s, other);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register/{other}/dispose", new { date = "2025-08-20", proceeds = 0m })).StatusCode);
        await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{other}/dispose", new { date = "2025-08-20", proceeds = 0m, gainLossAccountId = s["6900"] }));
        Assert.Equal(100m, await ClosingAsync(s, "6900"));
    }

    [Fact]
    public async Task Register_and_schedule_reports_come_from_what_was_posted()
    {
        var s = await NewAsync();
        var asset = await AssetAsync(s, 1_000m, 100m);
        var sold = await AssetAsync(s, 500m);
        var draft = await AssetAsync(s, 700m); // never capitalised: not in the register report
        await CapitaliseAsync(s, asset);
        await CapitaliseAsync(s, sold);
        await Ok(await Run(s, 2025, 2));
        await Ok(await Run(s, 2025, 3));
        await Ok(await s.C.PostAsJsonAsync($"{Assets}/register/{sold}/dispose", new { date = "2025-04-05", proceeds = 500m, proceedsAccountId = s["1010"] }));

        var march = await Ok(await s.C.GetAsync($"{Assets}/reports/register?asOf=2025-03-31&legalEntityId={s.Entity}"));
        Assert.Equal(2, march["rows"]!.AsArray().Count);
        Assert.Equal(1_500m, (decimal)march["totalCost"]!);
        Assert.Equal(180m + 100m, (decimal)march["totalAccumulatedDepreciation"]!);
        Assert.Equal(1_220m, (decimal)march["totalNetBookValue"]!);

        var february = await Ok(await s.C.GetAsync($"{Assets}/reports/register?asOf=2025-02-28&legalEntityId={s.Entity}"));
        Assert.Equal(1_360m, (decimal)february["totalNetBookValue"]!); // later runs do not change an earlier date

        var april = await Ok(await s.C.GetAsync($"{Assets}/reports/register?asOf=2025-04-30&legalEntityId={s.Entity}"));
        var row = april["rows"]!.AsArray().Single()!;
        Assert.Equal(asset, (string)row["assetId"]!);
        Assert.Equal("Ops team", (string)row["custodian"]!);
        Assert.Equal(820m, (decimal)row["netBookValue"]!);
        // Register net book value + nothing else equals what the ledger holds for these accounts.
        Assert.Equal((decimal)april["totalCost"]!, await ClosingAsync(s, "1500", "2025-04-30"));
        Assert.Equal(-(decimal)april["totalAccumulatedDepreciation"]!, await ClosingAsync(s, "1590", "2025-04-30"));

        var schedule = await Ok(await s.C.GetAsync($"{Assets}/register/{asset}/schedule"));
        var rows = schedule["rows"]!.AsArray();
        Assert.Equal(10, rows.Count);
        Assert.Equal((2025, 2, 90m, true), ((int)rows[0]!["year"]!, (int)rows[0]!["month"]!, (decimal)rows[0]!["amount"]!, (bool)rows[0]!["posted"]!));
        Assert.NotNull(rows[0]!["journalEntryId"]);
        Assert.False((bool)rows[2]!["posted"]!);
        Assert.Equal(100m, (decimal)rows[9]!["netBookValue"]!);

        // The generic list: filters and search come from the entity's attributes.
        var capitalised = await Ok(await s.C.GetAsync($"{Assets}/register?filter=legalEntityId:eq:{s.Entity}&filter=status:eq:capitalised"));
        Assert.Equal(asset, (string)capitalised.AsArray().Single()!["id"]!);
        Assert.Equal(820m, (decimal)capitalised.AsArray().Single()!["netBookValue"]!);
        Assert.Equal(2, (await Ok(await s.C.GetAsync($"{Assets}/register?filter=legalEntityId:eq:{s.Entity}&filter=status:ne:capitalised"))).AsArray().Count); // the sold one and the draft

        var months = await Ok(await s.C.GetAsync($"{Assets}/reports/schedule?from=2025-02-01&to=2025-05-31&legalEntityId={s.Entity}"));
        var byMonth = months["rows"]!.AsArray().ToDictionary(m => (int)m!["month"]!, m => ((decimal)m!["posted"]!, (decimal)m!["planned"]!));
        Assert.Equal((140m, 0m), byMonth[2]);
        Assert.Equal((0m, 90m), byMonth[4]); // the sold asset no longer plans anything
        Assert.Equal(280m, (decimal)months["totalPosted"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.GetAsync($"{Assets}/reports/schedule?from=2025-05-01&to=2025-02-01&legalEntityId={s.Entity}")).StatusCode);
        Assert.NotNull(draft);
    }

    [Fact]
    public async Task The_dashboard_shows_net_book_value_from_real_data_and_writes_reach_the_audit_trail()
    {
        var s = await NewAsync();
        var asset = await AssetAsync(s, 1_000m);
        await CapitaliseAsync(s, asset);
        await Ok(await Run(s, 2025, 3));

        var d = await Ok(await s.C.GetAsync($"/api/v1/dashboard?legalEntityId={s.Entity}"));
        var kpi = d["kpis"]!.AsArray().Single(k => (string?)k!["key"] == "assets-net-book-value")!;
        Assert.Equal(800m, (decimal)kpi["value"]!);

        var events = (await Ok(await s.C.GetAsync($"/api/v1/audit?filter=recordType:eq:FixedAsset&filter=recordId:eq:{asset}"))).AsArray();
        Assert.True(events.Count >= 3, $"Expected the create, the capitalisation and the run in the audit trail, found {events.Count}"); // create, capitalise, depreciation
    }

    [Fact]
    public async Task Assets_are_limited_by_module_permission_and_legal_entity_rules()
    {
        var s = await NewAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register", new { legalEntityId = s.Entity, name = "X", categoryId = s.Category, acquisitionDate = "2025-01-15", cost = 100m, salvageValue = 100m })).StatusCode); // salvage must stay under cost
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register", new { legalEntityId = Guid.NewGuid(), name = "X", categoryId = s.Category, acquisitionDate = "2025-01-15", cost = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/register", new { legalEntityId = s.Entity, name = "X", categoryId = Guid.NewGuid(), acquisitionDate = "2025-01-15", cost = 100m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Assets}/categories", new { code = "BAD" + Guid.NewGuid().ToString("N")[..4], name = "Wrong accounts", lifeMonths = 10, method = "straightLine", assetAccountId = s["6400"], accumulatedDepreciationAccountId = s["1590"], depreciationExpenseAccountId = s["6400"] })).StatusCode);

        var asset = await AssetAsync(s, 100m);
        var edited = await Ok(await s.C.PutAsJsonAsync($"{Assets}/register/{asset}", new { legalEntityId = s.Entity, name = "Renamed", categoryId = s.Category, acquisitionDate = "2025-01-15", cost = 120m }));
        Assert.Equal(120m, (decimal)edited["cost"]!);
        await CapitaliseAsync(s, asset);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PutAsJsonAsync($"{Assets}/register/{asset}", new { legalEntityId = s.Entity, name = "Renamed", categoryId = s.Category, acquisitionDate = "2025-01-15", cost = 999m })).StatusCode);
        Assert.Equal("Keeper", (string)(await Ok(await s.C.PutAsJsonAsync($"{Assets}/register/{asset}", new { legalEntityId = s.Entity, name = "Keeper", categoryId = s.Category, acquisitionDate = "2025-01-15", cost = 120m, custodian = "New team" })))["name"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.DeleteAsync($"{Assets}/register/{asset}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.DeleteAsync($"{Assets}/categories/{s.Category}")).StatusCode);

        var draft = await AssetAsync(s, 10m);
        Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"{Assets}/register/{draft}")).StatusCode);

        var suffix = Guid.NewGuid().ToString("N")[..10];
        var role = await Ok(await s.C.PostAsJsonAsync("/api/v1/roles", new { name = "AssetReader" + suffix, description = "test", permissions = new[] { "assets.register.read" } }), HttpStatusCode.Created);
        var email = $"a{suffix}@demo.local";
        (await s.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Reader", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)role["id"]! } })).EnsureSuccessStatusCode();
        var reader = (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Assets}/register/{asset}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"{Assets}/depreciation-runs", new { legalEntityId = s.Entity, year = 2025, month = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"{Assets}/register/{asset}/dispose", new { date = "2025-03-01", proceeds = 0m })).StatusCode);
    }

    [Fact]
    public async Task A_disabled_module_answers_403_and_keeps_its_data()
    {
        var s = await NewAsync();
        var asset = await AssetAsync(s, 10m);
        await s.C.PostAsync("/api/v1/modules/assets/disable", null);
        try
        {
            var (fresh, _) = await f.SignInAsync(ApiFactory.AdminEmail);
            Assert.Equal(HttpStatusCode.Forbidden, (await fresh.GetAsync($"{Assets}/register/{asset}")).StatusCode);
        }
        finally { await s.C.PostAsync("/api/v1/modules/assets/enable", null); }
        var (again, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync($"{Assets}/register/{asset}")).StatusCode);
    }
}
