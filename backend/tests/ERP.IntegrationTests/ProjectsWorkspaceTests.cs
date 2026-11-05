using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Projects: saving keeps what a form does not send, lists carry names and figures, one set of figures everywhere, access, approval and ledger rules.</summary>
public class ProjectsWorkspaceTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Prj = ProjectsTestKit.Prj;
    private static Task<JsonNode> Ok(HttpResponseMessage r, HttpStatusCode e = HttpStatusCode.OK) => ProjectsTestKit.Ok(r, e);
    private static string S(JsonNode n, string key) => (string)n[key]!;
    private static JsonArray Rows(JsonNode n) => n is JsonArray a ? a : n["items"]!.AsArray();

    private async Task<ProjectsTestKit> KitAsync()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        await k.C.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "TX" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5", kind = "vat", rate = 5, isDefault = true, legalEntityId = k.Entity });
        return k;
    }

    private static async Task<string> MeAsync(HttpClient c) => (await Ok(await c.GetAsync("/api/v1/me")))["user"]!["id"]!.ToString();

    /// <summary>A second person with exactly these permissions.</summary>
    private async Task<(HttpClient Client, string UserId)> PersonAsync(ProjectsTestKit k, params string[] permissions)
    {
        var role = await Ok(await k.C.PostAsJsonAsync("/api/v1/roles", new { name = "R " + Guid.NewGuid().ToString("N")[..6], description = "test", permissions }), HttpStatusCode.Created);
        var email = $"p{Guid.NewGuid().ToString("N")[..8]}@demo.local";
        await Ok(await k.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Second Person " + email[..5], temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)role["id"]! } }), HttpStatusCode.Created);
        var users = await ApiFactory.Json(await k.C.GetAsync("/api/v1/users"));
        var id = (string)(users is JsonArray list ? list : users["items"]!.AsArray()).First(u => (string?)u!["email"] == email)!["id"]!;
        var (client, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        return (client, id);
    }

    [Fact]
    public async Task Saving_with_only_the_basic_fields_keeps_everything_else_on_the_project()
    {
        var k = await KitAsync();
        var me = await MeAsync(k.C);
        var tax = S(await Ok(await k.C.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "TZ" + Guid.NewGuid().ToString("N")[..6], name = "Other", kind = "vat", rate = 7, legalEntityId = k.Entity }), HttpStatusCode.Created), "id");
        var created = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = k.Entity, name = "Full", status = "Active", billingType = "TimeAndMaterials", budgetAmount = 10m, customerId = k.Customer, managerUserId = me, budgetHours = 80m, taxCodeId = tax, sourceType = "sales.order", sourceId = Guid.NewGuid() }), HttpStatusCode.Created);
        var id = S(created, "id");

        // what an older form sends: no manager account, budget hours, tax code or source
        var saved = await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{id}", new { legalEntityId = k.Entity, name = "Renamed", status = "Active", billingType = "TimeAndMaterials", budgetAmount = 20m, customerId = k.Customer, managerName = S(created, "managerName") }));
        Assert.Equal("Renamed", S(saved, "name"));
        Assert.Equal(me, S(saved, "managerUserId"));
        Assert.Equal(80m, (decimal)saved["budgetHours"]!);
        Assert.Equal(tax, S(saved, "taxCodeId"));
        Assert.Equal("sales.order", S(saved, "sourceType"));

        var changed = await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{id}", new { legalEntityId = k.Entity, name = "Renamed", status = "Active", billingType = "TimeAndMaterials", budgetAmount = 20m, managerName = "Someone else", taxCodeId = Guid.Empty }));
        Assert.Null(changed["managerUserId"]);   // a different typed manager ends the link to the account
        Assert.Null(changed["taxCodeId"]);       // an empty id clears the tax code
    }

    [Fact]
    public async Task Lists_carry_project_task_and_milestone_names_and_the_project_list_its_figures()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var name = S(await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")), "name");
        var ms = await k.MilestoneAsync(p);
        var task = S(await Ok(await k.PostTask(p, ms), HttpStatusCode.Created), "id");
        await Ok(await k.PostTimesheet(p, task), HttpStatusCode.Created);

        var t = Rows(await Ok(await k.C.GetAsync($"{Prj}/tasks?filter=projectId:eq:{p}"))).Single()!;
        Assert.Equal(name, S(t, "projectName"));
        Assert.False(string.IsNullOrEmpty((string?)t["milestoneTitle"]));
        var time = Rows(await Ok(await k.C.GetAsync($"{Prj}/timesheets?filter=projectId:eq:{p}"))).Single()!;
        Assert.Equal(name, S(time, "projectName"));
        Assert.False(string.IsNullOrEmpty((string?)time["taskTitle"]));
        Assert.Equal(name, S(Rows(await Ok(await k.C.GetAsync($"{Prj}/milestones?filter=projectId:eq:{p}"))).Single()!, "projectName"));

        var row = Rows(await Ok(await k.C.GetAsync($"{Prj}/projects?search={Uri.EscapeDataString(name)}"))).First(r => S(r!, "id") == p)!;
        Assert.Equal("onTrack", S(row, "health"));
        Assert.Equal(1, (int)row["openTasks"]!);
    }

    [Fact]
    public async Task Templates_stay_out_of_the_work_lists_unless_their_own_project_is_asked_for()
    {
        var k = await KitAsync();
        var template = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = k.Entity, name = "Tpl", status = "Planning", billingType = "TimeAndMaterials", budgetAmount = 0m, isTemplate = true }), HttpStatusCode.Created), "id");
        var task = await k.TaskAsync(template);
        var live = Rows(await Ok(await k.C.GetAsync($"{Prj}/tasks?live=true&pageSize=200"))).Select(r => S(r!, "id")).ToList();
        Assert.DoesNotContain(task, live);
        Assert.Contains(task, Rows(await Ok(await k.C.GetAsync($"{Prj}/tasks?filter=projectId:eq:{template}"))).Select(r => S(r!, "id")));
    }

    [Fact]
    public async Task The_weekly_grid_changes_only_the_hours_of_an_entry()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var created = await Ok(await k.PostTimesheet(p, null, 2m, 77m, billable: false), HttpStatusCode.Created);
        var id = S(created, "id");
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/week", new { items = new[] { new { id, projectId = p, date = "2026-06-01", hours = 3m, isBillable = true } } }));
        var after = await Ok(await k.C.GetAsync($"{Prj}/timesheets/{id}"));
        Assert.Equal(3m, (decimal)after["hours"]!);
        Assert.Equal("Work", S(after, "description"));
        Assert.Equal(77m, (decimal)after["hourlyRate"]!);
        Assert.False((bool)after["isBillable"]!);
    }

    [Fact]
    public async Task Choosing_invoice_lines_bills_only_those_and_an_empty_list_means_none_of_that_kind()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        var a = S(await Ok(await k.PostTimesheet(p, null, 2m, 50m), HttpStatusCode.Created), "id");
        await Ok(await k.PostTimesheet(p, null, 1m, 50m), HttpStatusCode.Created);
        var ms = await k.MilestoneAsync(p, "Achieved", 100m);

        var work = await Ok(await k.C.GetAsync($"{Prj}/billing/{p}/billable"));
        Assert.Equal(2, work["time"]!.AsArray().Count);
        Assert.Single(work["milestones"]!.AsArray());

        var preview = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/preview", new { projectId = p, timesheetIds = new[] { a }, milestoneIds = Array.Empty<string>(), expenseIds = Array.Empty<string>() }));
        Assert.Equal(100m, (decimal)preview["subtotal"]!);
        Assert.Equal(0, (int)preview["milestones"]!);

        var bill = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p, timesheetIds = new[] { a }, milestoneIds = new[] { ms }, expenseIds = Array.Empty<string>() }));
        Assert.Equal(200m, (decimal)bill["totalAmountBilled"]!);
        var invoices = (await Ok(await k.C.GetAsync($"{Prj}/billing/{p}/invoices"))).AsArray();
        Assert.Equal(S(bill, "invoiceId"), S(invoices.Single()!, "id"));
    }

    [Fact]
    public async Task One_figure_for_unbilled_a_fixed_fee_project_does_not_wait_for_its_hours_and_can_be_completed()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync(billingType: "FixedFee");   // fee 1000
        await Ok(await k.PostTimesheet(p, null, 5m, 100m), HttpStatusCode.Created);
        await k.MilestoneAsync(p, "Achieved", 300m);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Travel", description = "Taxi", amount = 40m, markupPercent = 10m }), HttpStatusCode.Created);

        var project = await Ok(await k.C.GetAsync($"{Prj}/projects/{p}"));
        var overview = (await Ok(await k.C.GetAsync($"{Prj}/billing/overview"))).AsArray().Single(r => S(r!, "projectId") == p)!;
        Assert.Equal(344m, (decimal)project["unbilledAmount"]!);
        Assert.Equal((decimal)overview["totalUnbilledAmount"]!, (decimal)project["unbilledAmount"]!);

        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/projects/{p}/status", new { status = "Completed" }));
    }

    [Fact]
    public async Task Writing_off_a_project_uses_the_server_side_rule_and_moves_posted_cost_out_of_work_in_progress()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        var ts = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created), "id");
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));

        // once the cost is in the ledger the entry cannot silently change
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Prj}/timesheets/{ts}", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 9m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Prj}/timesheets/{ts}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Prj}/projects/{p}")).StatusCode);

        var off = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/write-off", new { projectId = p }));
        Assert.Equal(1, (int)off["writtenOff"]!);
        Assert.True((bool)(await Ok(await k.C.GetAsync($"{Prj}/timesheets/{ts}")))["writtenOff"]!);
        Assert.Equal(0m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/projects/{p}")))["unbilledAmount"]!);
    }

    [Fact]
    public async Task Voiding_an_invoice_puts_cost_back_into_work_in_progress_so_billing_again_releases_it_again()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/post-costs", new { projectId = p }));

        var invoice = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p })), "invoiceId");
        await Ok(await k.C.PostAsync($"/api/v1/finance/invoices/{invoice}/issue", null));
        await Ok(await k.C.PostAsJsonAsync($"/api/v1/finance/invoices/{invoice}/void", new { reason = "wrong customer" }));

        var journals = Rows(await Ok(await k.C.GetAsync($"/api/v1/finance/journals?pageSize=200&sort=-createdAt&search={Uri.EscapeDataString("cost back to work in progress")}")));
        Assert.NotEmpty(journals);

        var again = await Ok(await k.C.PostAsJsonAsync($"{Prj}/billing/bill", new { projectId = p }));
        Assert.Equal(500m, (decimal)again["totalAmountBilled"]!);
    }

    [Fact]
    public async Task People_cannot_approve_their_own_time_unless_the_setting_allows_it()
    {
        var k = await KitAsync();
        var me = await MeAsync(k.C);
        await Ok(await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.timesheetApproval"] = true }), HttpStatusCode.NoContent);
        try
        {
            var p = await k.ProjectAsync();
            var id = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-02", workerUserId = me, hours = 1m, hourlyRate = 10m }), HttpStatusCode.Created), "id");
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/submit", new { ids = new[] { id } }));
            Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/timesheets/decide", new { ids = new[] { id }, approve = true })).StatusCode);
            await Ok(await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.allowSelfApproval"] = true }), HttpStatusCode.NoContent);
            await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets/decide", new { ids = new[] { id }, approve = true }));
            Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Prj}/timesheets/{id}")).StatusCode);   // approved work needs rejecting first
        }
        finally
        {
            await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.timesheetApproval"] = false, ["projects.allowSelfApproval"] = false });
        }
    }

    [Fact]
    public async Task Cost_rates_stay_hidden_and_someone_elses_time_cannot_be_changed_without_rights()
    {
        var k = await KitAsync();
        var me = await MeAsync(k.C);
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        var id = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-03", workerUserId = me, hours = 1m }), HttpStatusCode.Created), "id");

        var (person, _) = await PersonAsync(k, "projects.projects.read", "projects.timesheets.read", "projects.timesheets.manage");
        var row = await Ok(await person.GetAsync($"{Prj}/timesheets/{id}"));
        Assert.Equal(0m, (decimal)row["costAmount"]!);
        Assert.Equal(0m, (decimal)Rows(await Ok(await person.GetAsync($"{Prj}/timesheets?filter=projectId:eq:{p}"))).Single()!["costRate"]!);
        Assert.Equal(60m, (decimal)(await Ok(await k.C.GetAsync($"{Prj}/timesheets/{id}")))["costRate"]!);

        Assert.Equal(HttpStatusCode.Forbidden, (await person.DeleteAsync($"{Prj}/timesheets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await person.PostAsJsonAsync($"{Prj}/timesheets/submit", new { ids = new[] { id } })).StatusCode);
    }

    [Fact]
    public async Task With_members_only_on_the_other_screens_and_direct_links_are_closed_too()
    {
        var k = await KitAsync();
        var mine = await k.ProjectAsync(); var other = await k.ProjectAsync();
        var otherTask = await k.TaskAsync(other);
        var otherMilestone = await k.MilestoneAsync(other);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/risks", new { projectId = other, kind = "Risk", title = "Secret risk", severity = "High", status = "Open" }), HttpStatusCode.Created);

        var (person, id) = await PersonAsync(k, "projects.projects.read", "projects.tasks.read", "projects.milestones.read", "projects.risks.read", "projects.reports.read", "projects.billing.read");
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects/{mine}/members", new { userId = id }), HttpStatusCode.Created);

        await Ok(await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.restrictToMembers"] = true }), HttpStatusCode.NoContent);
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync($"{Prj}/tasks/{otherTask}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync($"{Prj}/milestones/{otherMilestone}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync($"{Prj}/projects/{other}/schedule")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await person.GetAsync($"{Prj}/comments?ownerType=project&ownerId={other}")).StatusCode);
            Assert.DoesNotContain(other, Rows(await Ok(await person.GetAsync($"{Prj}/milestones?pageSize=200"))).Select(r => S(r!, "projectId")));
            Assert.Empty((await Ok(await person.GetAsync($"{Prj}/risks"))).AsArray());
            Assert.DoesNotContain(other, (await Ok(await person.GetAsync($"{Prj}/reports/budget"))).AsArray().Select(r => S(r!, "projectId")));
            Assert.DoesNotContain(other, (await Ok(await person.GetAsync($"{Prj}/billing/overview"))).AsArray().Select(r => S(r!, "projectId")));
        }
        finally { await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["projects.restrictToMembers"] = false }); }
    }

    [Fact]
    public async Task A_task_with_only_a_due_date_ends_on_it_and_the_plan_can_be_saved_as_a_baseline()
    {
        var k = await KitAsync();
        var res = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = k.Entity, name = "Dated", status = "Active", billingType = "TimeAndMaterials", budgetAmount = 0m, startDate = "2026-06-01", endDate = "2026-07-01" }), HttpStatusCode.Created);
        var p = S(res, "id");
        var task = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/tasks", new { projectId = p, title = "Dated", status = "Todo", priority = "Medium", estimatedHours = 16m, actualHours = 0m, dueDate = "2026-06-20" }), HttpStatusCode.Created), "id");
        await k.MilestoneAsync(p);

        var schedule = await Ok(await k.C.GetAsync($"{Prj}/projects/{p}/schedule"));
        var row = schedule["tasks"]!.AsArray().Single(r => S(r!, "id") == task)!;
        Assert.Equal("2026-06-20", S(row, "end"));
        Assert.Equal("2026-06-19", S(row, "start"));
        Assert.Single(schedule["milestones"]!.AsArray());
        Assert.False((bool)schedule["hasBaseline"]!);

        var saved = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects/{p}/baseline", new { clear = false }));
        Assert.True((bool)saved["hasBaseline"]!);
        Assert.Equal("2026-06-20", S(saved["tasks"]!.AsArray().Single()!, "baselineEnd"));
        var cleared = await Ok(await k.C.PostAsJsonAsync($"{Prj}/projects/{p}/baseline", new { clear = true }));
        Assert.False((bool)cleared["hasBaseline"]!);
    }

    [Fact]
    public async Task Budget_lines_compare_with_what_was_spent_and_the_activity_timeline_tells_what_happened()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/rates", new { projectId = p, billRate = 100m, costRate = 60m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = p, date = "2026-06-01", workerName = k.Worker, hours = 5m }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsJsonAsync($"{Prj}/expenses", new { projectId = p, date = "2026-06-01", category = "Travel", description = "Taxi", amount = 25m }), HttpStatusCode.Created);
        var line = S(await Ok(await k.C.PostAsJsonAsync($"{Prj}/budget-lines", new { projectId = p, category = "Labour", amount = 500m, hours = 10m }), HttpStatusCode.Created), "id");
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Prj}/budget-lines", new { projectId = p, category = "labour", amount = 1m })).StatusCode);

        var lines = (await Ok(await k.C.GetAsync($"{Prj}/projects/{p}/budget-lines"))).AsArray();
        var labour = lines.Single(l => S(l!, "category") == "Labour")!;
        Assert.Equal(300m, (decimal)labour["actual"]!);
        Assert.Equal(5m, (decimal)labour["actualHours"]!);
        Assert.Null(lines.Single(l => S(l!, "category") == "Travel")!["id"]);   // spent without a budget line
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{Prj}/budget-lines/{line}")).StatusCode);

        var task = await k.TaskAsync(p);
        await Ok(await k.C.PutAsJsonAsync($"{Prj}/tasks/{task}/status", new { status = "InProgress" }));
        var activity = (await Ok(await k.C.GetAsync($"{Prj}/projects/{p}/activity"))).AsArray();
        Assert.Contains(activity, a => S(a!, "kind") == "task" && S(a!, "action") == "Created");
        Assert.Contains(activity, a => S(a!, "kind") == "task" && a!["fields"]!.AsArray().Any(x => (string)x! == "Status"));

        var config = await Ok(await k.C.GetAsync($"{Prj}/config"));
        Assert.Equal(5, config["workDays"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_milestone_counts_in_the_period_it_was_reached()
    {
        var k = await KitAsync();
        var p = await k.ProjectAsync();
        await k.MilestoneAsync(p, "Achieved", 100m);   // achieved 2026-06-01 by the kit
        var june = (await Ok(await k.C.GetAsync($"{Prj}/reports/profitability?projectId={p}&from=2026-06-01&to=2026-06-30"))).AsArray().Single()!;
        var july = (await Ok(await k.C.GetAsync($"{Prj}/reports/profitability?projectId={p}&from=2026-07-01&to=2026-07-31"))).AsArray().Single()!;
        Assert.Equal(100m, (decimal)june["unbilled"]!);
        Assert.Equal(0m, (decimal)july["unbilled"]!);
    }
}
