using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Statutory payroll rules, daily attendance, employee history, onboarding checklists and recruitment.</summary>
public class HrWave4Tests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = HrKit.Hr;

    private static void PlainPay(JsonObject b) { b["housingAllowance"] = 0m; b["transportAllowance"] = 0m; b["recurringDeductions"] = 0m; }

    private static async Task<JsonNode> RuleAsync(HrKit kit, string code, string kind, decimal rate, string? nationalities)
        => await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new { legalEntityId = kit.Entity, code, name = code + " rule", kind, method = "flat", @base = "basic", rate, nationalities }), HttpStatusCode.Created);

    [Fact]
    public async Task Statutory_rules_take_the_employee_share_and_add_the_employer_cost_by_nationality()
    {
        var kit = await HrKit.StartAsync(f);
        var expat = await kit.EmployeeAsync(HrKit.Unique("Expat"), 3_000m, b => { PlainPay(b); b["nationality"] = "Indian"; });
        var citizen = await kit.EmployeeAsync(HrKit.Unique("Citizen"), 3_000m, b => { PlainPay(b); b["nationality"] = "Qatari"; });
        await RuleAsync(kit, "SS", "employeeDeduction", 5m, "!Qatari");
        await RuleAsync(kit, "SSE", "employerCost", 12.5m, "!Qatari");
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new { legalEntityId = kit.Entity, code = "ss", name = "Again", kind = "employeeDeduction", method = "flat", @base = "basic", rate = 1m })).StatusCode);

        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var lines = run["lines"]!.AsArray();
        var a = lines.Single(l => (string)l!["employeeId"]! == expat)!;
        var b = lines.Single(l => (string)l!["employeeId"]! == citizen)!;
        Assert.Equal(150m, (decimal)a["statutoryDeduction"]!);
        Assert.Equal(2_850m, (decimal)a["netPay"]!);
        Assert.Equal(375m, (decimal)a["employerCost"]!);
        Assert.Equal(0m, (decimal)b["statutoryDeduction"]!);
        Assert.Equal(3_000m, (decimal)b["netPay"]!);
        Assert.Equal(375m, (decimal)run["employerCost"]!);

        var id = (string)run["id"]!;
        var totals = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/payroll/{id}/statutory"))).AsArray();
        Assert.Equal(150m, (decimal)totals.Single(t => (string)t!["code"]! == "SS")!["amount"]!);
        Assert.Equal(375m, (decimal)totals.Single(t => (string)t!["code"]! == "SSE")!["amount"]!);
        Assert.Equal(1, (int)totals.Single(t => (string)t!["code"]! == "SS")!["employees"]!);

        var posted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{id}/post", null)); // the ledger entry balances or posting fails
        Assert.Equal("posted", (string)posted["status"]!);
    }

    [Fact]
    public async Task A_slab_rule_and_a_rule_that_ended_are_applied_by_month()
    {
        var kit = await HrKit.StartAsync(f);
        await kit.EmployeeAsync(HrKit.Unique("Slab"), 4_000m, PlainPay);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new
        {
            legalEntityId = kit.Entity, code = "TAX", name = "Income tax", kind = "employeeDeduction", method = "slab", @base = "gross",
            slabs = new[] { new { fromAmount = 0m, rate = 0m }, new { fromAmount = 1_000m, rate = 10m }, new { fromAmount = 3_000m, rate = 20m } },
        }), HttpStatusCode.Created);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new { legalEntityId = kit.Entity, code = "OLD", name = "Old levy", kind = "employeeDeduction", method = "flat", @base = "basic", rate = 10m, effectiveTo = "2026-09-30" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new { legalEntityId = kit.Entity, code = "BAD", name = "No slabs", kind = "employeeDeduction", method = "slab", @base = "basic" })).StatusCode);

        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var line = run["lines"]!.AsArray().Single()!;
        Assert.Equal(400m, (decimal)line["statutoryDeduction"]!);   // 2000 x 10% + 1000 x 20%; the old levy had ended
        Assert.Equal(3_600m, (decimal)line["netPay"]!);
    }

    [Fact]
    public async Task Daily_logs_work_out_late_and_overtime_and_roll_up_into_the_monthly_record_payroll_reads()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Daily"), 3_000m, PlainPay);
        var worked = await HrKit.Ok(await kit.Owner.PutAsJsonAsync($"{Hr}/attendance/daily", new { employeeId = emp, date = "2026-10-05", checkIn = "08:00:00", checkOut = "17:30:00" }));
        Assert.Equal("present", (string)worked["status"]!);
        Assert.Equal(570, (int)worked["workedMinutes"]!);
        Assert.Equal(90, (int)worked["overtimeMinutes"]!);
        var absent = await HrKit.Ok(await kit.Owner.PutAsJsonAsync($"{Hr}/attendance/daily", new { employeeId = emp, date = "2026-10-06" }));
        Assert.Equal("absent", (string)absent["status"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await kit.Owner.PutAsJsonAsync($"{Hr}/attendance/daily", new { employeeId = emp, date = "2026-10-07", checkIn = "09:00:00", checkOut = "08:00:00" })).StatusCode);

        var bulk = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/attendance/daily/bulk", new
        {
            items = new object[]
            {
                new { employeeId = emp, date = "2026-10-07", checkIn = "08:25:00", checkOut = "16:25:00" },
                new { employeeId = emp, date = "2019-12-31", checkIn = "08:00:00", checkOut = "16:00:00" },   // before the join date
            },
        }));
        Assert.Equal(1, (int)bulk["saved"]!);
        Assert.Single(bulk["rejected"]!.AsArray());

        var logs = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/attendance/daily?year=2026&month=10&employeeId={emp}"))).AsArray();
        Assert.Equal(3, logs.Count);
        Assert.Equal(25, (int)logs.Single(l => (string)l!["date"]! == "2026-10-07")!["lateMinutes"]!);

        var roll = (await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/attendance/daily/roll-up", new { year = 2026, month = 10, legalEntityId = kit.Entity }))).AsArray().Single()!;
        Assert.Equal(2, (int)roll["daysPresent"]!);
        Assert.Equal(1m, (decimal)roll["unpaidAbsentDays"]!);
        Assert.Equal(1.5m, (decimal)roll["overtimeHours"]!);
        Assert.Equal(1, (int)roll["lateDays"]!);

        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var line = run["lines"]!.AsArray().Single()!;
        Assert.Equal(1, (int)line["absentDays"]!);
        Assert.Equal(1.5m, (decimal)line["overtimeHours"]!);
        Assert.Equal(100m, (decimal)line["absence"]!);   // 3000 / 30 x 1 day

        var again = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/attendance/daily/roll-up", new { year = 2026, month = 10, legalEntityId = kit.Entity }));
        Assert.Single(again.AsArray()); // running it again updates the same record
    }

    [Fact]
    public async Task A_half_day_counts_as_attended_with_half_a_day_unpaid_and_payroll_deducts_half_a_day()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Half"), 3_000m, PlainPay);
        var half = await HrKit.Ok(await kit.Owner.PutAsJsonAsync($"{Hr}/attendance/daily", new { employeeId = emp, date = "2026-10-05", checkIn = "08:00:00", checkOut = "12:00:00", status = "halfDay" }));
        Assert.Equal("halfDay", (string)half["status"]!);
        Assert.Equal(240, (int)half["workedMinutes"]!);
        var roll = (await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/attendance/daily/roll-up", new { year = 2026, month = 10, legalEntityId = kit.Entity }))).AsArray().Single()!;
        Assert.Equal(1, (int)roll["daysPresent"]!);
        Assert.Equal(0.5m, (decimal)roll["unpaidAbsentDays"]!);
        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        Assert.Equal(50m, (decimal)run["lines"]!.AsArray().Single()!["absence"]!);   // 3000 / 30 x 0.5 day
    }

    [Fact]
    public async Task History_records_pay_and_role_changes_and_hides_pay_from_people_without_the_salary_permission()
    {
        var kit = await HrKit.StartAsync(f);
        var name = HrKit.Unique("Hist");
        var emp = await kit.EmployeeAsync(name, 3_000m);
        await HrKit.Ok(await kit.Owner.PutAsJsonAsync($"{Hr}/employees/{emp}", kit.EmployeeBody(name, 3_500m, b => b["designation"] = "Senior Agent")));
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/history", new { summary = "Completed the leasing course" }));

        var history = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/history"))).AsArray();
        Assert.Contains(history, h => (string)h!["kind"]! == "joined");
        var pay = history.Single(h => (string)h!["kind"]! == "salary")!;
        Assert.Equal("3000.00", (string)pay["oldValue"]!);
        Assert.Equal("3500.00", (string)pay["newValue"]!);
        Assert.Contains(history, h => (string)h!["kind"]! == "designation" && (string)h["newValue"]! == "Senior Agent");
        Assert.Contains(history, h => (string)h!["kind"]! == "note");

        var (reader, _) = await kit.UserAsync("HistReader", Perm.EmployeesRead);
        var masked = (await HrKit.Ok(await reader.GetAsync($"{Hr}/employees/{emp}/history"))).AsArray().Single(h => (string)h!["kind"]! == "salary")!;
        Assert.Null(masked["oldValue"]);
        Assert.Null(masked["newValue"]);

        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-15", reason = "Resigned" }));
        var after = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/history"))).AsArray();
        Assert.Contains(after, h => (string)h!["kind"]! == "terminated");
    }

    private static async Task<string> TemplateAsync(HrKit kit, string kind = "onboarding")
        => (string)(await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/checklists", new
        {
            name = HrKit.Unique("Starter "), kind,
            items = new[] { new { title = "Laptop and accounts", dueOffsetDays = 0 }, new { title = "Contract signed", dueOffsetDays = 7 } },
        }), HttpStatusCode.Created))["id"]!;

    [Fact]
    public async Task A_checklist_creates_dated_tasks_once_and_open_tasks_list_what_is_left()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Starter"), 3_000m, b => b["joinDate"] = "2026-11-02");
        var template = await TemplateAsync(kit);
        var tasks = (await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/employees/{emp}/checklists/{template}", null))).AsArray();
        Assert.Equal(2, tasks.Count);
        Assert.Equal("2026-11-02", (string)tasks.Single(t => (string)t!["title"]! == "Laptop and accounts")!["dueDate"]!);
        Assert.Equal("2026-11-09", (string)tasks.Single(t => (string)t!["title"]! == "Contract signed")!["dueDate"]!);
        Assert.Empty((await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/employees/{emp}/checklists/{template}", null))).AsArray()); // not twice

        var first = (string)tasks[0]!["id"]!;
        var done = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/tasks/{first}/done", new { done = true }));
        Assert.True((bool)done["isDone"]!);
        var open = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/tasks/open"))).AsArray().Where(t => (string)t!["employeeId"]! == emp).ToList();
        Assert.Single(open);
        var custom = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/tasks", new { title = "Order business cards", dueDate = "2026-11-20", kind = "onboarding" }));
        Assert.Equal("Order business cards", (string)custom["title"]!);
        Assert.Equal(3, (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/tasks"))).AsArray().Count);
    }

    [Fact]
    public async Task A_candidate_moves_through_the_pipeline_and_hiring_creates_the_employee_and_the_onboarding_tasks()
    {
        var kit = await HrKit.StartAsync(f);
        var template = await TemplateAsync(kit);
        var opening = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/openings", new { title = "Leasing agent", headcount = 1 }), HttpStatusCode.Created);
        var openingId = (string)opening["id"]!;
        var candidate = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/candidates", new { openingId, fullName = "Noor Hassan", email = "noor@example.com", expectedSalary = 4_000m }), HttpStatusCode.Created);
        var id = (string)candidate["id"]!;
        Assert.Equal("applied", (string)candidate["stage"]!);

        var hire = new { legalEntityId = kit.Entity, joinDate = "2026-12-01", designation = "Leasing agent", basicSalary = 4_000m, onboardingTemplateId = template };
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/hire", hire)).StatusCode); // no offer yet
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/move", new { stage = "interview" }));
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/move", new { stage = "screening" })).StatusCode); // no going back
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/move", new { stage = "hired" })).StatusCode);    // only through hire
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/move", new { stage = "offer", notes = "Offered 4000" }));

        var hired = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/hire", hire), HttpStatusCode.Created);
        Assert.Equal(2, (int)hired["tasksCreated"]!);
        var employeeId = (string)hired["employeeId"]!;
        var employee = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{employeeId}"));
        Assert.Equal("Noor Hassan", (string)employee["fullName"]!);
        Assert.Equal(4_000m, (decimal)employee["basicSalary"]!);
        Assert.Equal("hired", (string)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/candidates/{id}")))["stage"]!);
        Assert.Equal(1, (int)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/openings/{openingId}")))["hired"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/candidates/{id}/hire", hire)).StatusCode); // once

        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.DeleteAsync($"{Hr}/openings/{openingId}")).StatusCode); // it has candidates
    }
}
