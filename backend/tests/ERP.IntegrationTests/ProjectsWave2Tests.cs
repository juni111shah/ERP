using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Projects: release on void, closed-project guards, approval, rates and cost, expenses, planning, change orders, reports.</summary>
public class ProjectsWave2Tests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Prj = ProjectsTestKit.Prj;
    private static Task<JsonNode> Ok(HttpResponseMessage r, HttpStatusCode e = HttpStatusCode.OK) => ProjectsTestKit.Ok(r, e);
    private static string S(JsonNode n, string key) => (string)n[key]!;

    /// <summary>A kit whose workspace also has a default tax code, so invoices can be issued.</summary>
    private async Task<ProjectsTestKit> KitAsync()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        await k.C.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "TX" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5", kind = "vat", rate = 5, isDefault = true, legalEntityId = k.Entity });
        return k;
    }

    [Fact]
    public async Task Voiding_the_invoice_puts_time_expenses_and_milestones_back_so_they_can_be_billed_again()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var ts = S(await Ok(await k.PostTimesheet(p, hours: 2m, rate: 50m), HttpStatusCode.Created), "id");
        var ex = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Travel", description = "Taxi", amount = 40m, markupPercent = 10m }), HttpStatusCode.Created), "id");
        var ms = await k.MilestoneAsync(p, "Achieved", 100m);

        var bill = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));
        Assert.Equal(1, (int)bill["billedExpensesCount"]!);
        Assert.Equal(244m, (decimal)bill["totalAmountBilled"]!);   // 100 time + 44 expense with mark-up + 100 milestone
        var invoice = S(bill, "invoiceId");

        await Ok(await k.C.PostAsync($"/api/v1/finance/invoices/{invoice}/issue", null));
        await Ok(await k.C.PostAsJsonAsync($"/api/v1/finance/invoices/{invoice}/void", new { reason = "wrong customer" }));

        Assert.False((bool)(await Ok(await k.C.GetAsync($"{Prj}/timesheets/{ts}")))["isBilled"]!);
        Assert.False((bool)(await Ok(await k.C.GetAsync($"{Prj}/expenses/{ex}")))["isBilled"]!);
        Assert.Equal("achieved", S(await Ok(await k.C.GetAsync($"{Prj}/milestones/{ms}")), "status").ToLowerInvariant());
        var again = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));
        Assert.Equal(244m, (decimal)again["totalAmountBilled"]!);
    }

    [Fact]
    public async Task A_completed_project_cannot_be_edited_and_a_task_with_time_cannot_be_deleted()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var task = await k.TaskAsync(p);
        var ts = S(await Ok(await k.PostTimesheet(p, task, 3m, 10m), HttpStatusCode.Created), "id");
        Assert.Equal(3m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/tasks/{task}")))["actualHours"]!);   // actual hours follow the time logged
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Prj}/tasks/{task}")).StatusCode);

        // billable work blocks completion, written-off work does not
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" })).StatusCode);   // open task
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/tasks/{task}/status", new { status = "Cancelled" }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" })).StatusCode);   // unbilled time
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/write-off", new { ids = new[] { ts } }));
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" }));

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/tasks/{task}", new { projectId = p, title = "Changed", status = "Todo", priority = "Low", estimatedHours = 1m, actualHours = 0m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Prj}/timesheets/{ts}")).StatusCode);
    }

    [Fact]
    public async Task The_project_shows_the_customer_record_name_and_one_person_cannot_log_more_than_the_daily_limit()
    {
        var k = await KitAsync();
        var res = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = k.Entity, name = "Named", status = "Active", billingType = "TimeAndMaterials", budgetAmount = 0m, customerId = k.Customer, customerName = "Typed wrong" }), HttpStatusCode.Created);
        var expected = S(await Ok(await k.C.GetAsync($"/api/v1/parties/{k.Customer}")), "name");
        Assert.Equal(expected, S(res, "customerName"));

        var p = S(res, "id");
        await Ok(await k.PostTimesheet(p, null, 10m, 10m), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostTimesheet(p, null, 8m, 10m)).StatusCode);   // Ann would have 18 hours on that day; the limit is 16
        await Ok(await k.PostTimesheet(p, null, 6m, 10m), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2099-01-01", workerName = "Bob", hours = 1m, hourlyRate = 10m })).StatusCode);   // the future
    }

    [Fact]
    public async Task With_approval_on_time_starts_as_a_draft_and_is_billable_only_after_it_is_approved()
    {
        var k = await KitAsync();
        await Ok(await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.timesheetApproval"] = true }), HttpStatusCode.NoContent);
        try
        {
            var p = await k.ProjectAsync();
            var ts = await Ok(await k.PostTimesheet(p, null, 2m, 50m), HttpStatusCode.Created);
            Assert.Equal("draft", S(ts, "approvalStatus").ToLowerInvariant());
            Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p })).StatusCode);   // nothing approved yet
            var id = S(ts, "id");
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/submit", new { ids = new[] { id } }));
            Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/timesheets/{id}", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 3m, hourlyRate = 50m })).StatusCode);   // submitted is locked
            Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Prj}/timesheets/decide", new { ids = new[] { id }, approve = false })).StatusCode);   // a rejection needs a reason
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/decide", new { ids = new[] { id }, approve = false, reason = "Wrong project" }));
            Assert.Equal("rejected", S(await Ok(await k.C.GetAsync($"{Prj}/timesheets/{id}")), "approvalStatus").ToLowerInvariant());
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/submit", new { ids = new[] { id } }));
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/decide", new { ids = new[] { id }, approve = true }));
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));
        }
        finally { await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.timesheetApproval"] = false }); }
    }

    [Fact]
    public async Task Rates_come_from_the_rate_card_and_the_project_cost_is_what_the_hours_cost_not_what_they_bill()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 120m, costRate = 70m }), HttpStatusCode.Created);
        var ts = await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 2m }), HttpStatusCode.Created);
        Assert.Equal(120m, (decimal)ts["hourlyRate"]!);
        Assert.Equal(240m, (decimal)ts["totalAmount"]!);
        Assert.Equal(140m, (decimal)ts["costAmount"]!);
        var project = await Ok(await k.C.GetAsync($"{Prj}/projects/{p}"));
        Assert.Equal(140m, (decimal)project["totalTimesheetCost"]!);
        Assert.Equal(240m, (decimal)project["billableValue"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 1m, costRate = 1m })).StatusCode);   // same key twice
    }

    [Fact]
    public async Task Tasks_wait_for_each_other_loops_are_refused_and_the_schedule_marks_the_critical_path()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        async Task<string> Task(string title, decimal hours) => S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/tasks", new { projectId = p, title, status = "Todo", priority = "Medium", estimatedHours = hours, actualHours = 0m }), HttpStatusCode.Created), "id");
        var a = await Task("Design", 16m); var b = await Task("Build", 24m); var c = await Task("Side job", 8m);
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/tasks/{b}/dependencies", new { dependsOn = new[] { a } }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/tasks/{a}/dependencies", new { dependsOn = new[] { b } })).StatusCode);
        // "Build" cannot be Done before "Design"
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/tasks/{b}/status", new { status = "InProgress" }));
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/tasks/{b}/status", new { status = "Review" }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/tasks/{b}/status", new { status = "Done" })).StatusCode);

        var schedule = await Ok(await k.C.GetAsync($"{Prj}/projects/{p}/schedule"));
        var rows = schedule["tasks"]!.AsArray();
        var design = rows.Single(r => S(r!, "id") == a)!; var build = rows.Single(r => S(r!, "id") == b)!;
        Assert.True((bool)design["critical"]! && (bool)build["critical"]!);
        Assert.True(string.CompareOrdinal(S(build, "start"), S(design, "end")) > 0);   // Build starts after Design ends
    }

    [Fact]
    public async Task A_project_can_be_cloned_with_its_plan_and_a_subtask_belongs_to_one_parent()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var parent = await k.TaskAsync(p);
        var child = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/tasks", new { projectId = p, title = "Sub", status = "Todo", priority = "Low", estimatedHours = 1m, actualHours = 0m, parentTaskId = parent }), HttpStatusCode.Created), "id");
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Prj}/tasks", new { projectId = p, title = "Too deep", status = "Todo", priority = "Low", estimatedHours = 1m, actualHours = 0m, parentTaskId = child })).StatusCode);
        await k.MilestoneAsync(p);

        var copy = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects/{p}/clone", new { name = "Copy", startDate = "2026-09-01" }), HttpStatusCode.Created);
        Assert.Equal(2, (int)copy["totalTasks"]!);
        Assert.Equal(1, (int)copy["totalMilestones"]!);
        Assert.Equal("planning", S(copy, "status").ToLowerInvariant());
        Assert.NotEqual(S(await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")), "number"), S(copy, "number"));
    }

    [Fact]
    public async Task An_approved_change_order_raises_the_fee_and_milestones_cannot_pass_it()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync(billingType: "FixedFee");   // budget 1000
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, amount: 1200m)).StatusCode);
        var co = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/change-orders", new { projectId = p, title = "Extra scope", amountChange = 500m, hoursChange = 20m }), HttpStatusCode.Created), "id");
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, amount: 1200m)).StatusCode);   // still a draft
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/change-orders/{co}/decide", new { approve = true, note = "Agreed with the client" }));
        Assert.Equal(1500m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")))["effectiveBudget"]!);
        await Ok(await k.PostMilestone(p, amount: 1200m), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/change-orders/{co}/decide", new { approve = false })).StatusCode);   // decided once
    }

    [Fact]
    public async Task A_fixed_fee_can_be_billed_by_percentage_up_to_the_fee_with_a_preview_and_a_discount()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync(billingType: "FixedFee");   // fee 1000
        var preview = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/preview", new { projectId = p, progressPercent = 30m, discountPercent = 10m }));
        Assert.Equal(300m, (decimal)preview["subtotal"]!);
        Assert.Equal(30m, (decimal)preview["discount"]!);
        Assert.Equal(270m, (decimal)preview["total"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p, progressPercent = 130m })).StatusCode);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p, progressPercent = 30m }));
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p, progressPercent = 60m }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p, progressPercent = 20m })).StatusCode);   // 110 percent
        var overview = (await Ok(await k.C.GetAsync($"{Prj}/billing/overview"))).AsArray().Single(r => S(r!, "projectId") == p)!;
        Assert.Equal(100m, (decimal)overview["fixedFeeRemaining"]!);
    }

    [Fact]
    public async Task Reports_add_up_profit_utilisation_budget_and_ageing()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Materials", description = "Paint", amount = 100m, isBillable = false }), HttpStatusCode.Created);

        var profit = (await Ok(await k.C.GetAsync($"{Prj}/reports/profitability?projectId={p}"))).AsArray().Single()!;
        Assert.Equal(500m, (decimal)profit["unbilled"]!);
        Assert.Equal(400m, (decimal)profit["cost"]!);        // 300 labour + 100 expense
        Assert.Equal(100m, (decimal)profit["margin"]!);

        var util = (await Ok(await k.C.GetAsync($"{Prj}/reports/utilisation?from=2026-06-01&to=2026-06-07"))).AsArray();
        var ann = util.Single(r => S(r!, "person") == k.Worker)!;
        Assert.Equal(5m, (decimal)ann["loggedHours"]!);
        Assert.Equal(40m, (decimal)ann["capacityHours"]!);

        var budget = (await Ok(await k.C.GetAsync($"{Prj}/reports/budget"))).AsArray().Single(r => S(r!, "projectId") == p)!;
        Assert.Equal(1000m, (decimal)budget["budget"]!);
        Assert.Equal(400m, (decimal)budget["cost"]!);

        var ageing = (await Ok(await k.C.GetAsync($"{Prj}/reports/unbilled-ageing?asOf=2026-06-10"))).AsArray().Single(r => S(r!, "projectId") == p)!;
        Assert.Equal(500m, (decimal)ageing["current"]!);

        var hours = (await Ok(await k.C.GetAsync($"{Prj}/reports/hours?from=2026-06-01&to=2026-06-30&groupBy=person"))).AsArray();
        Assert.Equal(5m, (decimal)hours.Single(r => S(r!, "group") == k.Worker)!["hours"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.GetAsync($"{Prj}/reports/hours?from=2026-06-01&to=2026-06-30&groupBy=nonsense")).StatusCode);
    }

    [Fact]
    public async Task Project_cost_moves_to_work_in_progress_once_and_only_new_cost_is_posted_the_next_time()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created);   // cost 300
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Materials", description = "Paint", amount = 100m }), HttpStatusCode.Created);

        var first = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(2, (int)first["entries"]!);
        Assert.Equal(400m, (decimal)first["amount"]!);
        Assert.NotNull(first["journalId"]);

        var again = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(0, (int)again["entries"]!);                    // nothing new
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-02", category = "Travel", description = "Taxi", amount = 25m }), HttpStatusCode.Created);
        var next = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(25m, (decimal)next["amount"]!);
    }

    [Fact]
    public async Task Billing_work_whose_cost_was_posted_releases_work_in_progress_and_billing_still_works()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        var bill = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));   // posts Dr cost of sales, Cr work in progress inside the same transaction
        Assert.Equal(500m, (decimal)bill["totalAmountBilled"]!);
        var after = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));
        Assert.Equal(0, (int)after["entries"]!);
    }

    [Fact]
    public async Task A_sign_off_request_waits_for_the_customer_and_only_one_can_be_open_per_milestone()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var ms = await k.MilestoneAsync(p);
        var request = await Ok(await k.C.PostAsJsonAsync($"{Prj}/acceptances", new { projectId = p, milestoneId = ms, title = "Phase 1 delivered" }), HttpStatusCode.Created);
        Assert.Equal("pending", S(request, "status").ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/acceptances", new { projectId = p, milestoneId = ms, title = "Again" })).StatusCode);
        Assert.Single((await Ok(await k.C.GetAsync($"{Prj}/acceptances?projectId={p}"))).AsArray());
        // a staff sign-in is not a customer: the portal endpoints are closed to it
        Assert.NotEqual(HttpStatusCode.OK, (await k.C.GetAsync($"{Prj}/portal/projects")).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await k.C.PostAsJsonAsync($"{Prj}/portal/acceptances/{S(request, "id")}/sign", new { accept = true, signedByName = "Me", signature = "Me" })).StatusCode);
    }

    [Fact]
    public async Task Comments_risks_team_and_allocations_work_and_a_template_takes_no_time()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var c = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/comments", new { ownerType = "project", ownerId = p, text = "Kick-off held" }), HttpStatusCode.Created), "id");
        Assert.Single((await Ok(await k.C.GetAsync($"{Prj}/comments?ownerType=project&ownerId={p}"))).AsArray());
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{Prj}/comments/{c}")).StatusCode);

        await Ok(await k.C.PostAsJsonAsync($"{Prj}/risks", new { projectId = p, kind = "Risk", title = "Supplier delay", severity = "High", status = "Open" }), HttpStatusCode.Created);
        Assert.Single((await Ok(await k.C.GetAsync($"{Prj}/risks?projectId={p}"))).AsArray());

        var me = (await Ok(await k.C.GetAsync("/api/v1/me")))["user"]!["id"]!.ToString();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects/{p}/members", new { userId = me, role = "Lead" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/projects/{p}/members", new { userId = me })).StatusCode);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/allocations", new { projectId = p, userId = me, fromDate = "2026-06-01", toDate = "2026-06-30", hoursPerWeek = 20m }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Prj}/allocations", new { projectId = p, userId = me, fromDate = "2026-06-30", toDate = "2026-06-01", hoursPerWeek = 20m })).StatusCode);

        var template = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = k.Entity, name = "Template", status = "Planning", billingType = "TimeAndMaterials", budgetAmount = 0m, isTemplate = true }), HttpStatusCode.Created), "id");
        Assert.Equal(HttpStatusCode.Conflict, (await k.PostTimesheet(template)).StatusCode);
        await Ok(await k.PostTask(template), HttpStatusCode.Created);   // a template can hold tasks
    }
}
