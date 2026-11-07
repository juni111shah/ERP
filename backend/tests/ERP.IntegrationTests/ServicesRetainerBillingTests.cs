using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Services: who approves time, correcting it before approval, retainer periods and time-and-materials invoices through to the ledger, reference checks and the milestone cap.</summary>
public class ServicesRetainerBillingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Svc = "/api/v1/m/services";

    private static Task<JsonNode> Ok(HttpResponseMessage r, HttpStatusCode e = HttpStatusCode.OK) => ProjectsTestKit.Ok(r, e);

    private sealed record Setup(HttpClient C, string Entity, string Client);

    private async Task<Setup> NewAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/services/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "SvcBill " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var client = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Client " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", paymentTermsDays = 15 }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, client);
    }

    private static async Task<string> EngagementAsync(Setup s, int type = 2, decimal contractValue = 0m)
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Svc}/engagements", new { legalEntityId = s.Entity, title = "Engagement", clientId = s.Client, clientName = "typed", type, contractValue, budgetedHours = 100m, startDate = "2026-01-01" }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> LogAsync(Setup s, string engagement, string date, decimal hours, decimal rate = 100m, bool billable = true, Guid? consultant = null)
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Svc}/time-entries", new { legalEntityId = s.Entity, engagementId = engagement, consultantUserId = consultant ?? Guid.NewGuid(), consultantName = "Consultant", activityDate = date, hours, hourlyRate = rate, taskDescription = "Work", isBillable = billable }), HttpStatusCode.Created))["id"]!;

    private static async Task ApproveAsync(Setup s, params string[] ids)
    {
        foreach (var id in ids) await Ok(await s.C.PostAsync($"{Svc}/time-entries/{id}/approve", null));
    }

    private static async Task<string> RetainerAsync(Setup s, string engagement, decimal fee = 1000m, decimal included = 10m, decimal overage = 150m, int cycle = 1, string start = "2026-01-01")
        => (string)(await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers", new { legalEntityId = s.Entity, engagementId = engagement, clientId = s.Client, clientName = "typed", title = "Support", cycle, recurringFee = fee, includedHours = included, overageHourlyRate = overage, startDate = start }), HttpStatusCode.Created))["id"]!;

    /// <summary>Debit less credit per account of an invoice's journal, by system role.</summary>
    private static async Task<Dictionary<string, decimal>> InvoiceLedgerAsync(Setup s, string invoiceId)
    {
        var issued = await Ok(await s.C.PostAsync($"{Fin}/invoices/{invoiceId}/issue", null));
        var accounts = (await Ok(await s.C.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray().ToDictionary(a => (string)a!["id"]!, a => (string?)a!["systemRole"] ?? (string)a!["code"]!);
        var journal = await Ok(await s.C.GetAsync($"{Fin}/journals/{(string)issued["journalEntryId"]!}"));
        return journal["lines"]!.AsArray().GroupBy(l => accounts[(string)l!["accountId"]!]).ToDictionary(g => g.Key, g => g.Sum(l => (decimal)l!["debit"]! - (decimal)l!["credit"]!));
    }

    [Fact]
    public async Task The_approver_is_the_signed_in_user_never_the_one_named_in_the_body_and_cannot_be_the_worker()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s);
        var me = Guid.Parse((await Ok(await s.C.GetAsync("/api/v1/me")))["user"]!["id"]!.ToString());

        var theirs = await LogAsync(s, eng, "2026-06-01", 2m);
        var someoneElse = Guid.NewGuid();
        var approved = await Ok(await s.C.PostAsJsonAsync($"{Svc}/time-entries/{theirs}/approve", new { id = theirs, approvedByUserId = someoneElse })); // an older client's body
        Assert.Equal(me.ToString(), (string)approved["approvedByUserId"]!);
        Assert.True((bool)approved["isApproved"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Svc}/time-entries/{theirs}/approve", null)).StatusCode); // once

        var mine = await LogAsync(s, eng, "2026-06-01", 2m, consultant: me);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Svc}/time-entries/{mine}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/time-entries/{mine}/approve", new { id = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Time_can_be_corrected_or_deleted_until_it_is_approved_and_the_engagement_totals_follow()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s);
        var id = await LogAsync(s, eng, "2026-06-01", 3m, 100m);
        var other = await LogAsync(s, eng, "2026-06-02", 1m, 100m);

        var fixedEntry = await Ok(await s.C.PutAsJsonAsync($"{Svc}/time-entries/{id}", new { activityDate = "2026-06-03", hours = 5m, hourlyRate = 120m, taskDescription = "Corrected", isBillable = true }));
        Assert.Equal(600m, (decimal)fixedEntry["totalAmount"]!);
        var engagement = await Ok(await s.C.GetAsync($"{Svc}/engagements/{eng}"));
        Assert.Equal((6m, 700m), ((decimal)engagement["consumedHours"]!, (decimal)engagement["consumedCost"]!));

        Assert.Equal(HttpStatusCode.NoContent, (await s.C.DeleteAsync($"{Svc}/time-entries/{other}")).StatusCode);
        engagement = await Ok(await s.C.GetAsync($"{Svc}/engagements/{eng}"));
        Assert.Equal((5m, 600m), ((decimal)engagement["consumedHours"]!, (decimal)engagement["consumedCost"]!));

        await ApproveAsync(s, id);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PutAsJsonAsync($"{Svc}/time-entries/{id}", new { activityDate = "2026-06-03", hours = 1m, hourlyRate = 120m, taskDescription = "Too late", isBillable = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.DeleteAsync($"{Svc}/time-entries/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_cancelled_engagement_refuses_time_and_a_missing_one_is_an_error()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s);
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/engagements/{eng}/status", new { id = eng, status = "Cancelled" }));
        var body = new { legalEntityId = s.Entity, engagementId = eng, consultantUserId = Guid.NewGuid(), consultantName = "C", activityDate = "2026-06-01", hours = 1m, hourlyRate = 10m, taskDescription = "x", isBillable = true };
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Svc}/time-entries", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/time-entries", body with { engagementId = Guid.NewGuid().ToString() })).StatusCode);
    }

    [Fact]
    public async Task A_retainer_period_bills_the_fee_plus_overage_above_the_included_hours_once()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s, type: 3);
        var ret = await RetainerAsync(s, eng);
        // January: 12 approved billable hours (2 above the allowance); these do not count: unapproved, non-billable, another month
        await ApproveAsync(s, await LogAsync(s, eng, "2026-01-05", 8m), await LogAsync(s, eng, "2026-01-20", 4m), await LogAsync(s, eng, "2026-01-21", 50m, billable: false), await LogAsync(s, eng, "2026-02-02", 3m));
        await LogAsync(s, eng, "2026-01-30", 6m);

        var first = await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{ret}/invoice", new { periodDate = "2026-01-15" }));
        Assert.Equal("2026-01-01", (string)first["periodStart"]!);
        Assert.Equal("2026-01-31", (string)first["periodEnd"]!);
        Assert.Equal(1000m, (decimal)first["recurringFee"]!);
        Assert.Equal(12m, (decimal)first["billableHours"]!);
        Assert.Equal(2m, (decimal)first["overageHours"]!);
        Assert.Equal(300m, (decimal)first["overageAmount"]!);
        Assert.Equal(1300m, (decimal)first["net"]!);
        Assert.Equal(6m, (decimal)first["unapprovedHours"]!);
        Assert.False((bool)first["alreadyInvoiced"]!);
        var invoiceId = (string)first["invoiceId"]!;

        // idempotent per period, whichever date names it
        var again = await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{ret}/invoice", new { periodDate = "2026-01-31" }));
        Assert.True((bool)again["alreadyInvoiced"]!);
        Assert.Equal(invoiceId, (string)again["invoiceId"]!);

        var ledger = await InvoiceLedgerAsync(s, invoiceId);
        Assert.Equal((decimal)first["total"]!, ledger["ar.control"]);
        Assert.Equal(-1300m, ledger["income.services"]);

        // below the allowance February bills the fee alone, and late January approval rolls in here rather than getting lost
        var late = (await Ok(await s.C.GetAsync($"{Svc}/time-entries?pageSize=100&filter=engagementId:eq:{eng}"))).AsArray().Single(t => !(bool)t!["isApproved"]! && (decimal)t!["hours"]! == 6m)!;
        await ApproveAsync(s, (string)late["id"]!);
        var feb = await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{ret}/invoice", new { periodDate = "2026-02-10" }));
        Assert.Equal(9m, (decimal)feb["billableHours"]!);
        Assert.Equal(0m, (decimal)feb["overageAmount"]!);
        Assert.Equal(1000m, (decimal)feb["net"]!);
    }

    [Fact]
    public async Task Voiding_a_retainer_invoice_frees_the_period_and_its_hours()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s, type: 3);
        var ret = await RetainerAsync(s, eng, cycle: 2); // quarterly
        await ApproveAsync(s, await LogAsync(s, eng, "2026-02-05", 12m));
        var first = await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{ret}/invoice", new { periodDate = "2026-02-10" }));
        Assert.Equal(("2026-01-01", "2026-03-31"), ((string)first["periodStart"]!, (string)first["periodEnd"]!));
        Assert.Equal(1300m, (decimal)first["net"]!);

        await Ok(await s.C.PostAsync($"{Fin}/invoices/{(string)first["invoiceId"]!}/issue", null));
        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{(string)first["invoiceId"]!}/void", new { reason = "Wrong period" }));

        var second = await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{ret}/invoice", new { periodDate = "2026-02-10" }));
        Assert.False((bool)second["alreadyInvoiced"]!);
        Assert.NotEqual((string)first["invoiceId"]!, (string)second["invoiceId"]!);
        Assert.Equal(12m, (decimal)second["billableHours"]!);
    }

    [Fact]
    public async Task A_paused_retainer_or_a_period_not_started_raises_no_invoice()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s, type: 3);
        var next = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-01");
        var future = await RetainerAsync(s, eng, start: next);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Svc}/retainers/{future}/invoice", new { periodDate = next })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/retainers/{future}/invoice", new { periodDate = "2020-01-01" })).StatusCode);

        var running = await RetainerAsync(s, eng);
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/retainers/{running}/status", new { id = running, status = "Paused" }));
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsJsonAsync($"{Svc}/retainers/{running}/invoice", new { periodDate = "2026-01-15" })).StatusCode);
    }

    [Fact]
    public async Task Approved_billable_time_on_a_time_and_materials_engagement_is_invoiced_once_and_comes_back_on_void()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s);
        var a = await LogAsync(s, eng, "2026-03-01", 2m, 100m);
        var b = await LogAsync(s, eng, "2026-03-02", 1.5m, 200m);
        await LogAsync(s, eng, "2026-03-03", 9m); // not approved
        await ApproveAsync(s, a, b, await LogAsync(s, eng, "2026-03-04", 7m, billable: false));

        var invoice = await Ok(await s.C.PostAsync($"{Svc}/engagements/{eng}/invoice-time", null));
        Assert.Equal(500m, (decimal)invoice["net"]!);
        Assert.Equal(2, invoice["lines"]!.AsArray().Count);
        var id = (string)invoice["id"]!;
        Assert.Equal(id, (string)(await Ok(await s.C.GetAsync($"{Svc}/time-entries?pageSize=100&filter=engagementId:eq:{eng}"))).AsArray().Single(t => (string)t!["id"]! == a)!["invoiceId"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Svc}/engagements/{eng}/invoice-time", null)).StatusCode); // nothing left

        var ledger = await InvoiceLedgerAsync(s, id);
        Assert.Equal((decimal)invoice["total"]!, ledger["ar.control"]);
        Assert.Equal(-500m, ledger["income.services"]);

        await Ok(await s.C.PostAsJsonAsync($"{Fin}/invoices/{id}/void", new { reason = "Wrong rate" }));
        var chosen = await Ok(await s.C.PostAsJsonAsync($"{Svc}/engagements/{eng}/invoice-time", new { timeEntryIds = new[] { a } }));
        Assert.Equal(200m, (decimal)chosen["net"]!);

        var fixedPrice = await EngagementAsync(s, type: 1);
        await ApproveAsync(s, await LogAsync(s, fixedPrice, "2026-03-01", 2m));
        Assert.Equal(HttpStatusCode.Conflict, (await s.C.PostAsync($"{Svc}/engagements/{fixedPrice}/invoice-time", null)).StatusCode);
    }

    [Fact]
    public async Task References_are_checked_and_documents_show_the_name_on_the_customer_record()
    {
        var s = await NewAsync();
        var name = (string)(await Ok(await s.C.GetAsync($"/api/v1/parties/{s.Client}")))["name"]!;
        var eng = await EngagementAsync(s);
        Assert.Equal(name, (string)(await Ok(await s.C.GetAsync($"{Svc}/engagements/{eng}")))["clientName"]!);

        var vendor = (string)(await Ok(await s.C.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        foreach (var bad in new[] { vendor, Guid.NewGuid().ToString() })
            Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/engagements", new { legalEntityId = s.Entity, title = "X", clientId = bad, clientName = "X", type = 2, contractValue = 0m, budgetedHours = 0m, startDate = "2026-01-01" })).StatusCode);

        object Retainer(object engagementId, object clientId) => new { legalEntityId = s.Entity, engagementId, clientId, clientName = "x", title = "R", cycle = 1, recurringFee = 1m, includedHours = 0m, overageHourlyRate = 0m, startDate = "2026-01-01" };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/retainers", Retainer(Guid.NewGuid(), s.Client))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/retainers", Retainer(eng, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/milestones", new { legalEntityId = s.Entity, engagementId = Guid.NewGuid(), title = "M", amount = 5m, dueDate = "2026-06-01" })).StatusCode);
    }

    [Fact]
    public async Task Milestones_together_cannot_exceed_the_contract_value()
    {
        var s = await NewAsync();
        var eng = await EngagementAsync(s, type: 1, contractValue: 1000m);
        object Ms(decimal amount) => new { legalEntityId = s.Entity, engagementId = eng, title = "M", amount, dueDate = "2026-06-01" };
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", Ms(600m)), HttpStatusCode.Created);
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", Ms(400m)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.C.PostAsJsonAsync($"{Svc}/milestones", Ms(0.01m))).StatusCode);

        var open = await EngagementAsync(s, type: 1, contractValue: 0m); // no contract value, no cap
        await Ok(await s.C.PostAsJsonAsync($"{Svc}/milestones", new { legalEntityId = s.Entity, engagementId = open, title = "M", amount = 999_999m, dueDate = "2026-06-01" }), HttpStatusCode.Created);
    }
}
