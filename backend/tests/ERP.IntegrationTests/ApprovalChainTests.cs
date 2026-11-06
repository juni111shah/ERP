using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>
/// The approval engine: the approver permission of a policy, sequential levels with a decision history, maker-checker on manual journals and payments, and the
/// journal approval setting. Policies are workspace-wide, so each test removes its own and tests in a class never run in parallel.
/// </summary>
public class ApprovalChainTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Decide = "core.approvals.decide";

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
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Appr " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().ToDictionary(a => (string)a!["code"]!, a => (string)a!["id"]!);
        return new Setup(c, entity, accounts);
    }

    /// <summary>A signed-in user holding exactly these permissions.</summary>
    private async Task<HttpClient> UserAsync(Setup s, params string[] permissions)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var role = await Ok(await s.C.PostAsJsonAsync("/api/v1/roles", new { name = "Role " + suffix, description = "test", permissions }), HttpStatusCode.Created);
        var email = $"u{suffix}@demo.local";
        (await s.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "User " + suffix, temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)role["id"]! } })).EnsureSuccessStatusCode();
        return (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
    }

    private static async Task<string> PolicyAsync(Setup s, string documentType, string? permission = null, int? level = null, decimal? minAmount = null)
        => (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType, minAmount, approverPermission = permission, level }), HttpStatusCode.Created))["id"]!;

    private static async Task RemoveAsync(Setup s, params string[] policies)
    {
        foreach (var p in policies) await s.C.DeleteAsync($"/api/v1/approval-policies/{p}");
    }

    private static async Task<string> DraftJournalAsync(Setup s, decimal amount, string debit = "1010", string credit = "4000")
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = s.Entity, date = "2026-03-01", memo = "Adjust", lines = new[] { new { accountId = s[debit], debit = amount, credit = 0m }, new { accountId = s[credit], debit = 0m, credit = amount } } }), HttpStatusCode.Created))["id"]!;

    private static async Task<decimal> ClosingAsync(Setup s, string code)
    {
        var tb = await Ok(await s.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={s.Entity}"));
        return tb["rows"]!.AsArray().Where(r => (string)r!["code"]! == code).Sum(r => (decimal)r!["closing"]!);
    }

    private static Task<HttpResponseMessage> Approve(HttpClient c, string id, string comment = "ok") => c.PostAsJsonAsync($"/api/v1/approvals/{id}/approve", new { comment });
    private static Task<HttpResponseMessage> Reject(HttpClient c, string id, string comment = "no") => c.PostAsJsonAsync($"/api/v1/approvals/{id}/reject", new { comment });

    [Fact]
    public async Task The_approver_permission_of_the_policy_decides_who_may_approve()
    {
        var s = await NewAsync();
        var policy = await PolicyAsync(s, "journal", "finance.bank.write");
        try
        {
            var journal = await DraftJournalAsync(s, 100m);
            var requestId = (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null)))["approvalRequestId"]!;

            var generalApprover = await UserAsync(s, Decide);
            Assert.Equal(HttpStatusCode.Forbidden, (await Approve(generalApprover, requestId)).StatusCode);
            Assert.DoesNotContain((await Ok(await generalApprover.GetAsync("/api/v1/approvals"))).AsArray(), r => (string)r!["id"]! == requestId);

            var rightApprover = await UserAsync(s, "finance.bank.write");
            Assert.Contains((await Ok(await rightApprover.GetAsync("/api/v1/approvals"))).AsArray(), r => (string)r!["id"]! == requestId);
            var approved = await Ok(await Approve(rightApprover, requestId));
            Assert.Equal("approved", (string)approved["status"]!);
            Assert.Null(approved["currentLevel"]);
        }
        finally { await RemoveAsync(s, policy); }
    }

    [Fact]
    public async Task A_policy_must_name_a_real_permission_and_level()
    {
        var s = await NewAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "journal", approverPermission = "no.such.permission" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "journal", level = 0 })).StatusCode);
        var created = await Ok(await s.C.PostAsJsonAsync("/api/v1/approval-policies", new { documentType = "payment", minAmount = 10m }), HttpStatusCode.Created);
        Assert.Equal(Decide, (string)created["approverPermission"]!); // defaults keep working
        Assert.Equal(1, (int)created["level"]!);
        await RemoveAsync(s, (string)created["id"]!);
    }

    [Fact]
    public async Task Levels_decide_in_order_and_each_decision_is_kept()
    {
        var s = await NewAsync();
        var (first, second) = (await PolicyAsync(s, "journal", Decide, 1), await PolicyAsync(s, "journal", "finance.bank.write", 2));
        try
        {
            var journal = await DraftJournalAsync(s, 500m);
            var waiting = await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null));
            Assert.Equal("draft", (string)waiting["status"]!);
            var requestId = (string)waiting["approvalRequestId"]!;

            var levelOne = await UserAsync(s, Decide);
            var levelTwo = await UserAsync(s, "finance.bank.write");
            var both = await UserAsync(s, Decide, "finance.bank.write");

            // Level 2 cannot decide before level 1, and does not see it in its queue yet.
            var early = await Approve(levelTwo, requestId);
            Assert.Equal(HttpStatusCode.Forbidden, early.StatusCode);
            Assert.Contains("Level 1", await early.Content.ReadAsStringAsync());
            Assert.DoesNotContain((await Ok(await levelTwo.GetAsync("/api/v1/approvals"))).AsArray(), r => (string)r!["id"]! == requestId);

            var afterOne = await Ok(await Approve(both, requestId, "first look"));
            Assert.Equal("pending", (string)afterOne["status"]!);
            Assert.Equal(2, (int)afterOne["currentLevel"]!);
            Assert.Equal("finance.bank.write", (string)afterOne["currentApproverPermission"]!);

            // Still not postable, and one person cannot be both levels.
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/journals/{journal}/post", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Approve(both, requestId)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Approve(levelOne, requestId)).StatusCode); // level 1 has had its turn

            var done = await Ok(await Approve(levelTwo, requestId, "second look"));
            Assert.Equal("approved", (string)done["status"]!);
            Assert.Equal("second look", (string)done["decisionComment"]!);

            var history = await Ok(await s.C.GetAsync($"/api/v1/approvals/{requestId}"));
            var steps = history["steps"]!.AsArray();
            Assert.Equal(2, steps.Count);
            Assert.Equal((1, "approved", "first look"), ((int)steps[0]!["level"]!, (string)steps[0]!["status"]!, (string)steps[0]!["comment"]!));
            Assert.Equal((2, "approved", "second look"), ((int)steps[1]!["level"]!, (string)steps[1]!["status"]!, (string)steps[1]!["comment"]!));
            Assert.All(steps, step => Assert.NotNull(step!["decidedBy"]));

            var posted = await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null));
            Assert.Equal("posted", (string)posted["status"]!);
            Assert.Equal(500m, await ClosingAsync(s, "1010"));
        }
        finally { await RemoveAsync(s, first, second); }
    }

    [Fact]
    public async Task A_rejection_at_any_level_ends_the_chain_and_amount_thresholds_pick_the_levels()
    {
        var s = await NewAsync();
        var (first, second) = (await PolicyAsync(s, "journal", Decide, 1), await PolicyAsync(s, "journal", "finance.bank.write", 2, 1_000m));
        try
        {
            // Under the threshold of level 2: a single level.
            var small = await DraftJournalAsync(s, 100m);
            var smallRequest = (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{small}/post", null)))["approvalRequestId"]!;
            var approver = await UserAsync(s, Decide);
            Assert.Equal("approved", (string)(await Ok(await Approve(approver, smallRequest)))["status"]!);

            var big = await DraftJournalAsync(s, 5_000m);
            var bigRequest = (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{big}/post", null)))["approvalRequestId"]!;
            var rejected = await Ok(await Reject(approver, bigRequest, "Wrong account"));
            Assert.Equal("rejected", (string)rejected["status"]!);
            var steps = rejected["steps"]!.AsArray();
            Assert.Equal(("rejected", "cancelled"), ((string)steps[0]!["status"]!, (string)steps[1]!["status"]!));
            Assert.Equal(0m, await ClosingAsync(s, "1010")); // approved or not, nothing posted yet

            // Posting again sends it for a fresh approval; editing withdraws a pending one.
            var again = await Ok(await s.C.PostAsync($"{Fin}/journals/{big}/post", null));
            Assert.NotEqual(bigRequest, (string)again["approvalRequestId"]!);
            await Ok(await s.C.PutAsJsonAsync($"{Fin}/journals/{big}", new { legalEntityId = s.Entity, date = "2026-03-01", lines = new[] { new { accountId = s["1010"], debit = 4_000m, credit = 0m }, new { accountId = s["4000"], debit = 0m, credit = 4_000m } } }));
            var mine = (await Ok(await s.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray();
            Assert.Equal("withdrawn", (string)mine.Single(r => (string)r!["id"]! == (string)again["approvalRequestId"]!)!["status"]!);
            Assert.Null((await Ok(await s.C.GetAsync($"{Fin}/journals/{big}")))["approvalRequestId"]);
        }
        finally { await RemoveAsync(s, first, second); }
    }

    [Fact]
    public async Task A_journal_under_a_policy_stays_out_of_the_ledger_until_approved_by_someone_else()
    {
        var s = await NewAsync();
        var policy = await PolicyAsync(s, "journal", minAmount: 1_000m);
        try
        {
            var under = await DraftJournalAsync(s, 999m); // under the threshold: behaviour as before
            Assert.Equal("posted", (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{under}/post", null)))["status"]!);

            var journal = await DraftJournalAsync(s, 1_000m);
            var waiting = await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null));
            Assert.Equal("draft", (string)waiting["status"]!);
            Assert.StartsWith("DRAFT-", (string)waiting["number"]!);
            var requestId = (string)waiting["approvalRequestId"]!;
            Assert.Equal(999m, await ClosingAsync(s, "1010"));
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/journals/{journal}/post", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Approve(s.C, requestId)).StatusCode); // the maker cannot approve

            await Ok(await Approve(await UserAsync(s, Decide), requestId));
            Assert.Equal(999m, await ClosingAsync(s, "1010")); // approving does not post
            Assert.Equal("posted", (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null)))["status"]!);
            Assert.Equal(1_999m, await ClosingAsync(s, "1010"));
        }
        finally { await RemoveAsync(s, policy); }
    }

    [Fact]
    public async Task The_journal_approval_setting_gates_journals_without_a_policy()
    {
        var s = await NewAsync();
        var plain = await DraftJournalAsync(s, 10m);
        Assert.Equal("posted", (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{plain}/post", null)))["status"]!); // off by default

        Assert.Equal(HttpStatusCode.NoContent, (await s.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["core.journalRequiresApproval"] = true })).StatusCode);
        try
        {
            var journal = await DraftJournalAsync(s, 25m);
            var waiting = await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null));
            Assert.Equal("draft", (string)waiting["status"]!);
            Assert.Equal(10m, await ClosingAsync(s, "1010"));
            await Ok(await Approve(await UserAsync(s, Decide), (string)waiting["approvalRequestId"]!));
            Assert.Equal("posted", (string)(await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null)))["status"]!);
            Assert.Equal(35m, await ClosingAsync(s, "1010"));
        }
        finally { await s.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["core.journalRequiresApproval"] = false }); }
    }

    private static async Task<(string Vendor, string Bill)> VendorBillAsync(Setup s, decimal price)
    {
        var vendor = (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        var bill = await Ok(await s.C.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = s.Entity, partyId = vendor, billDate = "2026-03-01", lines = new[] { new { description = "Goods", quantity = 1m, price, accountId = s["6900"] } } }), HttpStatusCode.Created);
        await Ok(await s.C.PostAsync($"{Fin}/bills/{(string)bill["id"]!}/post", null));
        return (vendor, (string)bill["id"]!);
    }

    private static object Payment(Setup s, string vendor, string bill, decimal amount)
        => new { legalEntityId = s.Entity, partyId = vendor, date = "2026-03-10", method = "bankTransfer", bankAccountId = s["1010"], amount, reference = "PAY-1", allocations = new[] { new { billId = bill, amount } } };

    [Fact]
    public async Task A_payment_under_a_policy_moves_no_money_until_approved_and_posted()
    {
        var s = await NewAsync();
        var (vendor, bill) = await VendorBillAsync(s, 800m);
        var policy = await PolicyAsync(s, "payment", minAmount: 500m);
        try
        {
            var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, vendor, bill, 800m)), HttpStatusCode.Created);
            Assert.Equal("pendingApproval", (string)payment["status"]!);
            Assert.Null(payment["journalEntryId"]);
            Assert.StartsWith("DRAFT-", (string)payment["number"]!);
            var (id, requestId) = ((string)payment["id"]!, (string)payment["approvalRequestId"]!);

            Assert.Equal(0m, await ClosingAsync(s, "1010"));
            Assert.Equal(-800m, await ClosingAsync(s, "2000"));
            Assert.Equal("issued", (string)(await Ok(await s.C.GetAsync($"{Fin}/bills/{bill}")))["status"]!);
            Assert.Equal(800m, (decimal)(await Ok(await s.C.GetAsync($"{Fin}/reports/ap-ageing?asOf=2026-12-31&legalEntityId={s.Entity}")))["totals"]!["total"]!);

            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/payments/{id}/post", null)).StatusCode); // still waiting
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Fin}/payments/{id}/void", new { reason = "Not posted" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await Approve(s.C, requestId)).StatusCode); // the maker cannot approve
            await Ok(await Approve(await UserAsync(s, Decide), requestId));
            Assert.Equal(0m, await ClosingAsync(s, "1010"));

            var posted = await Ok(await s.C.PostAsync($"{Fin}/payments/{id}/post", null));
            Assert.Equal("issued", (string)posted["status"]!);
            Assert.StartsWith("PAY-", (string)posted["number"]!);
            Assert.NotNull(posted["journalEntryId"]);
            Assert.Single(posted["allocations"]!.AsArray());
            Assert.Equal(-800m, await ClosingAsync(s, "1010"));
            Assert.Equal(0m, await ClosingAsync(s, "2000"));
            Assert.Equal("paid", (string)(await Ok(await s.C.GetAsync($"{Fin}/bills/{bill}")))["status"]!);
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Fin}/payments/{id}/post", null)).StatusCode); // only once
        }
        finally { await RemoveAsync(s, policy); }
    }

    [Fact]
    public async Task A_rejected_payment_can_go_again_or_be_deleted_and_small_payments_post_at_once()
    {
        var s = await NewAsync();
        var (vendor, bill) = await VendorBillAsync(s, 800m);
        var policy = await PolicyAsync(s, "payment", minAmount: 500m);
        try
        {
            var small = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, vendor, bill, 100m)), HttpStatusCode.Created);
            Assert.Equal("issued", (string)small["status"]!);
            Assert.Null(small["approvalRequestId"]);

            var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, vendor, bill, 700m)), HttpStatusCode.Created);
            var (id, requestId) = ((string)payment["id"]!, (string)payment["approvalRequestId"]!);
            await Ok(await Reject(await UserAsync(s, Decide), requestId));

            var again = await Ok(await s.C.PostAsync($"{Fin}/payments/{id}/post", null));
            Assert.Equal("pendingApproval", (string)again["status"]!);
            var secondRequest = (string)again["approvalRequestId"]!;
            Assert.NotEqual(requestId, secondRequest);

            Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"{Fin}/payments/{id}")).StatusCode);
            Assert.Equal("withdrawn", (string)(await Ok(await s.C.GetAsync($"/api/v1/approvals/{secondRequest}")))["status"]!);
            Assert.Equal(HttpStatusCode.NotFound, (await s.C.GetAsync($"{Fin}/payments/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await s.C.DeleteAsync($"{Fin}/payments/{(string)small["id"]!}")).StatusCode); // a posted one is voided, not deleted
            Assert.Equal(-100m, await ClosingAsync(s, "1010"));

            // A bad allocation fails now, not after someone has approved it.
            Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, vendor, bill, 900m))).StatusCode);
        }
        finally { await RemoveAsync(s, policy); }
    }

    [Fact]
    public async Task Without_a_policy_journals_and_payments_behave_as_before()
    {
        var s = await NewAsync();
        var journal = await DraftJournalAsync(s, 50m);
        var posted = await Ok(await s.C.PostAsync($"{Fin}/journals/{journal}/post", null));
        Assert.Equal("posted", (string)posted["status"]!);
        Assert.Null(posted["approvalRequestId"]);

        var (vendor, bill) = await VendorBillAsync(s, 80m);
        var payment = await Ok(await s.C.PostAsJsonAsync($"{Fin}/payments", Payment(s, vendor, bill, 80m)), HttpStatusCode.Created);
        Assert.Equal("issued", (string)payment["status"]!);
        Assert.Null(payment["approvalRequestId"]);
    }
}
