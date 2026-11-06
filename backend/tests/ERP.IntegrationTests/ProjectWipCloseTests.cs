using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Project cost in the ledger: journals follow the date of the work, non-billable cost skips work in progress, closing a project empties it, and the fee stands apart from the cost budget.</summary>
public class ProjectWipCloseTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Prj = ProjectsTestKit.Prj;
    private const string Fin = "/api/v1/finance";
    private const string Wip = "1350", Cogs = "5000", Recovery = "5080";
    private static Task<JsonNode> Ok(HttpResponseMessage r, HttpStatusCode e = HttpStatusCode.OK) => ProjectsTestKit.Ok(r, e);

    /// <summary>Debits less credits per account code, over every journal whose memo names the project.</summary>
    private static async Task<Dictionary<string, decimal>> LedgerAsync(ProjectsTestKit k, string projectId)
    {
        var number = (string)(await Ok(await k.C.GetAsync($"{Prj}/projects/{projectId}")))["number"]!;
        var rows = (await Ok(await k.C.GetAsync($"{Fin}/journals?pageSize=200&search={Uri.EscapeDataString("Project " + number)}"))).AsArray();
        var net = new Dictionary<string, decimal>();
        foreach (var row in rows)
            foreach (var line in (await Ok(await k.C.GetAsync($"{Fin}/journals/{(string)row!["id"]!}")))["lines"]!.AsArray())
            {
                var code = (string)line!["accountCode"]!;
                net[code] = net.GetValueOrDefault(code) + (decimal)line["debit"]! - (decimal)line["credit"]!;
            }
        return net;
    }

    private static async Task<string> TimeAsync(ProjectsTestKit k, string project, string date, decimal hours, bool billable = true)
        => (string)(await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = project, date, workerName = k.Worker, hours, isBillable = billable }), HttpStatusCode.Created))["id"]!;

    private static async Task RatesAsync(ProjectsTestKit k, string project) => await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = project, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);

    [Fact]
    public async Task Completing_a_fixed_fee_project_moves_the_remaining_work_in_progress_to_cost_of_sales()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync(billingType: "FixedFee");
        await RatesAsync(k, p);
        await TimeAsync(k, p, "2026-06-01", 5m); // cost 300, never billed on a fixed fee
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Materials", description = "Paint", amount = 100m, isBillable = false }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));

        var before = await LedgerAsync(k, p);
        Assert.Equal(300m, before[Wip]);
        Assert.Equal(100m, before[Cogs]); // the non-billable expense never went through work in progress
        Assert.Equal(300m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")))["wipBalance"]!);

        var done = await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" }));
        Assert.Equal(0m, (decimal)done["wipBalance"]!);

        var after = await LedgerAsync(k, p);
        Assert.Equal(0m, after[Wip]);
        Assert.Equal(400m, after[Cogs]);
        Assert.Equal(-400m, after[Recovery]);
    }

    [Fact]
    public async Task Cancelling_a_project_also_empties_work_in_progress_and_a_later_void_does_not_refill_it()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        await RatesAsync(k, p);
        await TimeAsync(k, p, "2026-06-01", 5m);
        await TimeAsync(k, p, "2026-06-02", 2m);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));

        var invoice = (string)(await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p })))["invoiceId"]!;
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{invoice}/issue", null));
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Cancelled" }));
        Assert.Equal(0m, (await LedgerAsync(k, p))[Wip]);

        await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Customer walked away" }));
        Assert.Equal(0m, (await LedgerAsync(k, p))[Wip]);
        Assert.Equal(420m, (await LedgerAsync(k, p))[Cogs]);
    }

    [Fact]
    public async Task Cost_journals_carry_the_date_of_the_work_one_per_month()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        await RatesAsync(k, p);
        await TimeAsync(k, p, "2026-03-10", 1m);
        await TimeAsync(k, p, "2026-03-20", 2m);
        await TimeAsync(k, p, "2026-05-05", 3m);

        var posted = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(3, (int)posted["entries"]!);
        Assert.Equal(360m, (decimal)posted["amount"]!);
        var journals = posted["journals"]!.AsArray();
        Assert.Equal(2, journals.Count);
        Assert.Equal(("2026-03-20", 180m), ((string)journals[0]!["date"]!, (decimal)journals[0]!["toWorkInProgress"]!));
        Assert.Equal(("2026-05-05", 180m), ((string)journals[1]!["date"]!, (decimal)journals[1]!["toWorkInProgress"]!));

        var march = await Ok(await k.C.GetAsync($"{Fin}/journals/{(string)journals[0]!["journalId"]!}"));
        Assert.Equal("2026-03-20", (string)march["date"]!);
        Assert.Equal(180m, (decimal)march["lines"]!.AsArray().Single(l => (string)l!["accountCode"]! == Wip)!["debit"]!);
    }

    [Fact]
    public async Task Non_billable_cost_goes_to_expense_and_never_touches_work_in_progress()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var internalProject = await k.ProjectAsync(billingType: "NonBillable");
        await RatesAsync(k, internalProject);
        await TimeAsync(k, internalProject, "2026-06-01", 5m);
        var whole = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = internalProject }));
        Assert.Equal(0m, (decimal)whole["toWorkInProgress"]!);
        Assert.Equal(300m, (decimal)whole["toExpense"]!);
        var ledger = await LedgerAsync(k, internalProject);
        Assert.False(ledger.ContainsKey(Wip));
        Assert.Equal(300m, ledger[Cogs]);
        Assert.Equal(0m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/projects/{internalProject}")))["wipBalance"]!);

        // on a billable project only the non-billable entry skips work in progress
        var mixed = await k.ProjectAsync();
        await RatesAsync(k, mixed);
        await TimeAsync(k, mixed, "2026-06-01", 5m, billable: false);
        await TimeAsync(k, mixed, "2026-06-01", 1m);
        var split = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = mixed }));
        Assert.Equal((60m, 300m), ((decimal)split["toWorkInProgress"]!, (decimal)split["toExpense"]!));
        Assert.Single(split["journals"]!.AsArray()); // same month, one journal
        var billed = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = mixed }));
        Assert.Equal(100m, (decimal)billed["totalAmountBilled"]!); // the billable hour; releasing it leaves nothing behind
        var mixedLedger = await LedgerAsync(k, mixed);
        Assert.Equal(0m, mixedLedger[Wip]);
        Assert.Equal(360m, mixedLedger[Cogs]);
    }

    [Fact]
    public async Task Cost_posted_after_a_project_closed_goes_straight_to_cost_of_sales()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync(billingType: "FixedFee");
        await RatesAsync(k, p);
        await TimeAsync(k, p, "2026-06-01", 5m);
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" }));

        var late = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(300m, (decimal)late["toExpense"]!);
        Assert.False((await LedgerAsync(k, p)).ContainsKey(Wip));
    }

    [Fact]
    public async Task The_contract_value_sets_the_fee_cap_billing_and_margin_while_the_budget_stays_the_cost_budget()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var body = new { legalEntityId = k.Entity, name = "Fee " + Guid.NewGuid().ToString("N")[..6], status = "Active", billingType = "FixedFee", budgetAmount = 600m, contractValue = 1000m, customerId = k.Customer };
        var created = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", body), HttpStatusCode.Created);
        var p = (string)created["id"]!;
        Assert.Equal(1000m, (decimal)created["contractValue"]!);
        Assert.Equal(600m, (decimal)created["budgetAmount"]!);
        Assert.Equal(600m, (decimal)created["effectiveBudget"]!); // health still measures cost against the cost budget

        // milestones are capped by the contract value, not the budget
        await Ok(await k.PostMilestone(p, amount: 900m), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, amount: 200m)).StatusCode);

        // percentage billing works from the contract value
        var preview = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/preview", new { projectId = p, progressPercent = 10m }));
        Assert.Equal(100m, (decimal)preview["total"]!);
        var work = await Ok(await k.C.GetAsync($"{Prj}/billing/{p}/billable"));
        Assert.Equal(1000m, (decimal)work["fee"]!);

        await RatesAsync(k, p);
        await TimeAsync(k, p, "2026-06-01", 5m); // cost 300
        var margin = (await Ok(await k.C.GetAsync($"{Prj}/reports/profitability?projectId={p}"))).AsArray().Single()!;
        Assert.Equal(1000m, (decimal)margin["contractValue"]!);
        Assert.Equal(700m, (decimal)margin["contractMargin"]!);
        Assert.Equal(70m, (decimal)margin["contractMarginPercent"]!);
        var budget = (await Ok(await k.C.GetAsync($"{Prj}/reports/budget"))).AsArray().Single(r => (string)r!["projectId"]! == p)!;
        Assert.Equal(600m, (decimal)budget["budget"]!);
        Assert.Equal(1000m, (decimal)budget["contractValue"]!);
        Assert.Equal(700m, (decimal)budget["forecastMargin"]!);

        // a form that does not send it keeps it
        var saved = await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}", new { legalEntityId = k.Entity, name = "Renamed", status = "Active", billingType = "FixedFee", budgetAmount = 650m, customerId = k.Customer }));
        Assert.Equal(1000m, (decimal)saved["contractValue"]!);
    }

    [Fact]
    public async Task Without_a_contract_value_the_budget_is_still_the_fee()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync(billingType: "FixedFee"); // budget 1000, no contract value
        Assert.Null((await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")))["contractValue"]);
        await Ok(await k.PostMilestone(p, amount: 1000m), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, amount: 1m)).StatusCode);
        Assert.Equal(1000m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/billing/{p}/billable")))["fee"]!);
        var margin = (await Ok(await k.C.GetAsync($"{Prj}/reports/profitability?projectId={p}"))).AsArray().Single()!;
        Assert.Equal(1000m, (decimal)margin["contractValue"]!);
    }
}
