using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Leave management: types, holidays, working-day counting, balances, approval flow, overlap, attendance feed into payroll, permissions.</summary>
public class HrLeaveTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string EmployeeId, string Annual, string Unpaid);

    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<Ctx> SetupAsync(decimal basic = 3_000m)
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Leave " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var employee = (string)(await Ok(await c.PostAsJsonAsync($"{Hr}/employees", new { legalEntityId = entity, fullName = "Leave Tester", designation = "Agent", joinDate = "2020-01-01", status = "active", basicSalary = basic, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m }), HttpStatusCode.Created))["id"]!;
        var annual = (string)(await Ok(await c.PostAsJsonAsync($"{Hr}/leave-types", new { code = Code("AN"), name = "Annual", isPaid = true, annualDays = 10m, carryForwardCap = 5m, requiresApproval = true }), HttpStatusCode.Created))["id"]!;
        var unpaid = (string)(await Ok(await c.PostAsJsonAsync($"{Hr}/leave-types", new { code = Code("UP"), name = "Unpaid", isPaid = false, annualDays = 0m, carryForwardCap = 0m, requiresApproval = true }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity, employee, annual, unpaid);
    }

    private static object Req(Ctx x, string type, string from, string to, bool submit = false, string? reason = null) => new { employeeId = x.EmployeeId, leaveTypeId = type, fromDate = from, toDate = to, reason, submit };

    private static async Task<JsonNode> BalanceAsync(Ctx x, int year, string typeId)
    {
        var rows = (await Ok(await x.C.GetAsync($"{Hr}/leave/balances?year={year}&employeeId={x.EmployeeId}"))).AsArray();
        return rows.Single(r => (string)r!["leaveTypeId"]! == typeId)!;
    }

    [Fact]
    public async Task Working_days_skip_weekends_and_public_holidays()
    {
        var x = await SetupAsync();
        // 2031-03-03 is a Monday; Saturday and Sunday are the default weekend
        Assert.Equal(5, (int)(await Ok(await x.C.GetAsync($"{Hr}/leave/days?from=2031-03-03&to=2031-03-07")))["workingDays"]!);
        Assert.Equal(6, (int)(await Ok(await x.C.GetAsync($"{Hr}/leave/days?from=2031-03-03&to=2031-03-10")))["workingDays"]!);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/holidays", new { date = "2031-03-05", name = "Test holiday" }), HttpStatusCode.Created);
        Assert.Equal(4, (int)(await Ok(await x.C.GetAsync($"{Hr}/leave/days?from=2031-03-03&to=2031-03-07")))["workingDays"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/holidays", new { date = "2031-03-05", name = "Again" })).StatusCode);
        Assert.Contains((await Ok(await x.C.GetAsync($"{Hr}/holidays?pageSize=100"))).AsArray(), h => (string)h!["date"]! == "2031-03-05");
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.GetAsync($"{Hr}/leave/days?from=2031-03-07&to=2031-03-03")).StatusCode);
    }

    [Fact]
    public async Task A_request_is_submitted_approved_against_the_balance_and_cancelling_gives_the_days_back()
    {
        var x = await SetupAsync();
        // April 2031: Mon 7 - Fri 11 and Mon 14 - Fri 18 are 5 working days each; the balance is 10
        var first = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-04-07", "2031-04-11", submit: true, reason: "Family")), HttpStatusCode.Created);
        Assert.Equal("submitted", (string)first["status"]!);
        Assert.Equal(5m, (decimal)first["days"]!);
        var firstId = (string)first["id"]!;
        var b = await BalanceAsync(x, 2031, x.Annual);
        Assert.Equal(5m, (decimal)b["pending"]!);
        Assert.Equal(0m, (decimal)b["taken"]!);

        var approved = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{firstId}/approve", new { comment = "Enjoy" }));
        Assert.Equal("approved", (string)approved["status"]!);
        b = await BalanceAsync(x, 2031, x.Annual);
        Assert.Equal(5m, (decimal)b["taken"]!);
        Assert.Equal(5m, (decimal)b["remaining"]!);

        // overlap with the approved one is refused
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-04-09", "2031-04-10", submit: true))).StatusCode);
        // a second week uses the rest, a third request has nothing left
        var second = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-04-14", "2031-04-18", submit: true)), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{(string)second["id"]!}/approve", new { }));
        Assert.Equal(0m, (decimal)(await BalanceAsync(x, 2031, x.Annual))["remaining"]!);
        var third = await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-04-21", "2031-04-22", submit: true));
        Assert.Equal(HttpStatusCode.Conflict, third.StatusCode);
        Assert.Contains("balance", await third.Content.ReadAsStringAsync());

        // cancelling an approved request restores the days
        var cancelled = await Ok(await x.C.PostAsync($"{Hr}/leave/{firstId}/cancel", null));
        Assert.Equal("cancelled", (string)cancelled["status"]!);
        Assert.Equal(5m, (decimal)(await BalanceAsync(x, 2031, x.Annual))["remaining"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Hr}/leave/{firstId}/cancel", null)).StatusCode);

        // the requester is told about the decision
        var notes = (await Ok(await x.C.GetAsync("/api/v1/notifications")))["items"]!.AsArray();
        Assert.Contains(notes, n => ((string)n!["title"]!).Contains("leave request was approved"));
    }

    [Fact]
    public async Task Drafts_can_be_edited_rejections_keep_the_balance_and_weekend_only_requests_are_refused()
    {
        var x = await SetupAsync();
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-05-05", "2031-05-06")), HttpStatusCode.Created); // Mon-Tue
        Assert.Equal("draft", (string)draft["status"]!);
        var id = (string)draft["id"]!;
        var edited = await Ok(await x.C.PutAsJsonAsync($"{Hr}/leave/{id}", Req(x, x.Annual, "2031-05-05", "2031-05-07")));
        Assert.Equal(3m, (decimal)edited["days"]!);
        await Ok(await x.C.PostAsync($"{Hr}/leave/{id}/submit", null));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Hr}/leave/{id}", Req(x, x.Annual, "2031-05-05", "2031-05-06"))).StatusCode); // no longer a draft

        var rejected = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{id}/reject", new { comment = "Busy period" }));
        Assert.Equal("rejected", (string)rejected["status"]!);
        Assert.Equal("Busy period", (string)rejected["decisionComment"]!);
        Assert.Equal(0m, (decimal)(await BalanceAsync(x, 2031, x.Annual))["taken"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/leave/{id}/approve", new { })).StatusCode); // already decided

        // 2031-05-10 and 11 are Saturday and Sunday
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-05-10", "2031-05-11"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-05-12", "2031-05-09"))).StatusCode);
    }

    [Fact]
    public async Task A_type_without_approval_is_approved_on_submit()
    {
        var x = await SetupAsync();
        var sick = (string)(await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave-types", new { code = Code("SK"), name = "Sick", isPaid = true, annualDays = 15m, carryForwardCap = 0m, requiresApproval = false }), HttpStatusCode.Created))["id"]!;
        var r = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, sick, "2031-07-01", "2031-07-02", submit: true)), HttpStatusCode.Created); // Tue-Wed
        Assert.Equal("approved", (string)r["status"]!);
        Assert.Equal(2m, (decimal)(await BalanceAsync(x, 2031, sick))["taken"]!);
        // a leave type code can only be used once (case-insensitive)
        var dup = Code("DU");
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave-types", new { code = dup, name = "First", isPaid = true, annualDays = 1m, carryForwardCap = 0m }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/leave-types", new { code = dup.ToLowerInvariant(), name = "Second", isPaid = true, annualDays = 1m, carryForwardCap = 0m })).StatusCode);
    }

    [Fact]
    public async Task Approved_leave_shows_in_attendance_and_unpaid_leave_reduces_payroll()
    {
        var x = await SetupAsync(basic: 3_000m);
        // June 2031: Tue 3 - Wed 4 unpaid, Tue 10 - Wed 11 paid
        var unpaid = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Unpaid, "2031-06-03", "2031-06-04", submit: true)), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{(string)unpaid["id"]!}/approve", new { }));
        var paid = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-06-10", "2031-06-11", submit: true)), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{(string)paid["id"]!}/approve", new { }));
        // a submitted (not yet approved) request must not count
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Unpaid, "2031-06-17", "2031-06-18", submit: true)), HttpStatusCode.Created);

        var row = (await Ok(await x.C.GetAsync($"{Hr}/attendance?year=2031&month=6&legalEntityId={x.Entity}"))).AsArray().Single(r => (string)r!["employeeId"]! == x.EmployeeId)!;
        Assert.Equal(2, (int)row["leaveUnpaidDays"]!);
        Assert.Equal(2, (int)row["leavePaidDays"]!);
        Assert.False((bool)row["recorded"]!); // derived days do not create a manual record

        // a manual absence adds to the leave-derived unpaid days
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = x.EmployeeId, year = 2031, month = 6, workingDays = 22, daysPresent = 21, unpaidAbsentDays = 1, paidLeaveDays = 0, overtimeHours = 0m }));
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2031, month = 6 }), HttpStatusCode.Created);
        var line = run["lines"]!.AsArray().Single()!;
        Assert.Equal(3, (int)line["absentDays"]!); // 1 recorded + 2 unpaid leave
        Assert.Equal(300m, (decimal)line["absence"]!); // 3 x (3,000 / 30)
    }

    [Fact]
    public async Task Leave_endpoints_need_the_right_permissions()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Hr}/leave")).StatusCode);
        var x = await SetupAsync();
        var roles = (await Ok(await x.C.GetAsync("/api/v1/roles"))).AsArray();
        var viewer = (string)roles.First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var email = $"lv{Guid.NewGuid():N}"[..10] + "@demo.local";
        (await x.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Plain User", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { viewer } })).EnsureSuccessStatusCode();
        var (plain, _) = await f.SignInAsync(email, "Temp#Passw0rd", workspaceCode: "demo-re");
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.GetAsync($"{Hr}/leave")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-08-04", "2031-08-05"))).StatusCode);

        var pending = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(x, x.Annual, "2031-08-04", "2031-08-05", submit: true)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.PostAsJsonAsync($"{Hr}/leave/{(string)pending["id"]!}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plain.PostAsJsonAsync($"{Hr}/leave-types", new { code = "X", name = "X", isPaid = true, annualDays = 1m, carryForwardCap = 0m })).StatusCode);
    }
}
