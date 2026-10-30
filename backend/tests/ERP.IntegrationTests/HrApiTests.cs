using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>HR module end to end: employees, attendance, payroll posting and payment, approval, gratuity provision and the cash forecast.</summary>
public class HrApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Bank);

    /// <summary>A fresh legal entity in the UAE real estate workspace with the HR module switched on.</summary>
    private async Task<Ctx> SetupAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var enable = await first.PostAsync("/api/v1/modules/hr/enable", null);
        Assert.True(enable.IsSuccessStatusCode, await enable.Content.ReadAsStringAsync());
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re"); // sign in again: permissions of a newly enabled module are in the next token
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Staff " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        return new Ctx(c, entity, bank);
    }

    private static object Employee(Ctx x, string name, decimal basic, decimal housing = 0m, decimal transport = 0m, decimal deductions = 0m, string join = "2020-01-01", string? number = null)
        => new { legalEntityId = x.Entity, number, fullName = name, designation = "Agent", department = "Leasing", joinDate = join, status = "active", basicSalary = basic, housingAllowance = housing, transportAllowance = transport, otherAllowances = 0m, recurringDeductions = deductions, iban = "AE070331234567890123456" };

    private static async Task<decimal> ClosingAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2030-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    [Fact]
    public async Task Employees_are_numbered_validated_and_carry_gratuity_under_the_country_rule()
    {
        var x = await SetupAsync();
        var created = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Amir Khan", 6_000m, 2_500m, 800m, 500m, join: "2024-04-15")), HttpStatusCode.Created);
        Assert.StartsWith("EMP-", (string)created["number"]!);
        Assert.Equal(9_300m, (decimal)created["grossMonthly"]!);
        Assert.True((decimal)created["gratuityAccrued"]! > 10_000m); // 2.5 years x 21 days of 6,000 basic

        var id = (string)created["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "No pay", 0m))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Dup", 100m, number: (string)created["number"]!))).StatusCode);
        var updated = await Ok(await x.C.PutAsJsonAsync($"{Hr}/employees/{id}", Employee(x, "Amir Khan", 6_500m, 2_500m, 800m, 500m, join: "2024-04-15")));
        Assert.Equal(9_800m, (decimal)updated["grossMonthly"]!);

        var listed = await Ok(await x.C.GetAsync($"{Hr}/employees?search=Amir"));
        Assert.Contains(listed.AsArray(), e => (string?)e!["id"] == id);
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Hr}/employees/{id}")).StatusCode);
    }

    [Fact]
    public async Task Attendance_exceptions_flow_into_the_payroll_and_the_run_posts_and_pays_through_the_ledger()
    {
        var x = await SetupAsync();
        var amir = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Amir Khan", 6_000m, 2_500m, 800m, 500m)), HttpStatusCode.Created);
        var neha = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Neha Kapoor", 12_000m)), HttpStatusCode.Created);

        var before = (await Ok(await x.C.GetAsync($"{Hr}/attendance?year=2026&month=10&legalEntityId={x.Entity}"))).AsArray();
        Assert.Equal(2, before.Count);
        Assert.All(before, r => Assert.False((bool)r!["recorded"]!)); // nothing recorded = full attendance
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = (string)amir["id"]!, year = 2026, month = 10, workingDays = 26, daysPresent = 30, unpaidAbsentDays = 0, paidLeaveDays = 0, overtimeHours = 0 })).StatusCode);
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = (string)amir["id"]!, year = 2026, month = 10, workingDays = 26, daysPresent = 24, unpaidAbsentDays = 2, paidLeaveDays = 0, overtimeHours = 10 }));

        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10, plannedPayDate = "2026-10-29" }), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        Assert.StartsWith("PR-", (string)run["number"]!);
        Assert.Equal(2, (int)run["employees"]!);
        var amirLine = run["lines"]!.AsArray().Single(l => (string)l!["employeeName"]! == "Amir Khan")!;
        Assert.Equal((620m, 312.50m, 500m), ((decimal)amirLine["absence"]!, (decimal)amirLine["overtime"]!, (decimal)amirLine["deductions"]!));
        Assert.Equal(9_300m - 620m + 312.50m - 500m, (decimal)amirLine["netPay"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10 })).StatusCode); // one run per month

        // a one-off adjustment survives a recalculation
        var adjusted = await Ok(await x.C.PutAsJsonAsync($"{Hr}/payroll/{id}/lines/{(string)amirLine["id"]!}", new { adjustment = 200m, note = "Bonus" }));
        var recalculated = await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/recalculate", null));
        Assert.Equal((decimal)adjusted["netPay"]!, (decimal)recalculated["netPay"]!);
        var net = (decimal)recalculated["netPay"]!; var gross = (decimal)recalculated["grossEarnings"]!; var deductions = (decimal)recalculated["deductions"]!;
        Assert.Equal(gross - deductions, net);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-29" })).StatusCode); // not posted yet
        var posted = await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
        Assert.Equal("posted", (string)posted["status"]!);
        Assert.Equal(gross, await ClosingAsync(x, "6000"));
        Assert.Equal(-net, await ClosingAsync(x, "2300"));
        Assert.Equal(-deductions, await ClosingAsync(x, "1250"));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Hr}/payroll/{id}/lines/{(string)amirLine["id"]!}", new { adjustment = 1m })).StatusCode); // posted runs are fixed

        var file = await x.C.GetAsync($"{Hr}/payroll/{id}/bank-file");
        var csv = await file.Content.ReadAsStringAsync();
        Assert.Contains("Amir Khan", csv);
        Assert.Contains("AE070331234567890123456", csv);

        var paid = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-29" }));
        Assert.Equal(("paid", "2026-10-29"), ((string)paid["status"]!, (string)paid["paidOn"]!));
        Assert.Equal(0m, await ClosingAsync(x, "2300"));
        Assert.Equal(-net, await ClosingAsync(x, "1010"));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-30" })).StatusCode);
    }

    [Fact]
    public async Task A_draft_payroll_can_be_deleted_and_an_empty_entity_has_nothing_to_pay()
    {
        var x = await SetupAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 1 })).StatusCode); // no employees
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Only", 3_000m)), HttpStatusCode.Created);
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 1 }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Hr}/payroll/{(string)run["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 1 })).StatusCode);
    }

    [Fact]
    public async Task Payroll_waits_for_approval_when_the_setting_is_on()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Approval Staff", 5_000m)), HttpStatusCode.Created);
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 3 }), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.payrollRequiresApproval"] = true })).StatusCode);
        try
        {
            var pending = await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
            Assert.Equal("pendingApproval", (string)pending["status"]!);
            Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Hr}/payroll/{id}/post", null)).StatusCode);

            var roles = await Ok(await x.C.GetAsync("/api/v1/roles"));
            var approverRole = (string)roles.AsArray().First(r => (string?)r!["name"] == "Approver")!["id"]!;
            var email = $"hr{Guid.NewGuid():N}"[..10] + "@demo.local";
            (await x.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Approver", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { approverRole } })).EnsureSuccessStatusCode();
            var approver = (await f.SignInAsync(email, "Temp#Passw0rd", "demo-re")).Client;
            await Ok(await approver.PostAsJsonAsync($"/api/v1/approvals/{(string)pending["approvalRequestId"]!}/approve", new { comment = "ok" }));
            Assert.Equal("posted", (string)(await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null)))["status"]!);
        }
        finally { await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.payrollRequiresApproval"] = false }); }
    }

    [Fact]
    public async Task The_gratuity_report_uses_the_uae_rule_and_the_provision_trues_up_once()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Long Service", 18_000m, join: "2016-01-01")), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "New Joiner", 9_000m, join: "2026-08-01")), HttpStatusCode.Created);

        var report = await Ok(await x.C.GetAsync($"{Hr}/reports/gratuity?asOf=2026-10-22&legalEntityId={x.Entity}"));
        Assert.Contains("UAE", (string)report["rule"]!);
        var rows = report["rows"]!.AsArray();
        var longRow = rows.Single(r => (string)r!["employee"]! == "Long Service")!;
        Assert.True((bool)longRow["eligible"]!);
        Assert.InRange((decimal)longRow["entitlementDays"]!, 275m, 285m); // 10.8 years: 105 + 5.8 x 30 = 279
        Assert.False((bool)rows.Single(r => (string)r!["employee"]! == "New Joiner")!["eligible"]!);
        var liability = (decimal)report["totalLiability"]!;
        Assert.Equal((decimal)longRow["gratuity"]!, liability);

        var first = await Ok(await x.C.PostAsJsonAsync($"{Hr}/reports/gratuity/provision", new { legalEntityId = x.Entity, asOf = "2026-10-22" }));
        Assert.Equal(liability, (decimal)first["posted"]!);
        Assert.Equal(-liability, await ClosingAsync(x, "2500"));
        var again = await Ok(await x.C.PostAsJsonAsync($"{Hr}/reports/gratuity/provision", new { legalEntityId = x.Entity, asOf = "2026-10-22" }));
        Assert.Equal(0m, (decimal)again["posted"]!); // nothing left to true up
    }

    [Fact]
    public async Task Hr_appears_on_the_dashboard_and_in_the_cash_forecast_only_when_enabled()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Forecast", 4_000m)), HttpStatusCode.Created);
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = DateTime.UtcNow.Year, month = DateTime.UtcNow.Month }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));

        var dashboard = await Ok(await x.C.GetAsync("/api/v1/dashboard"));
        Assert.Contains(dashboard["kpis"]!.AsArray(), k => (string?)k!["key"] == "employees");
        var headcount = dashboard["charts"]!.AsArray().First(ch => (string?)ch!["key"] == "hr-headcount")!["series"]![0]!["values"]!.AsArray().Select(v => (decimal)v!).ToList();
        Assert.Equal(3, headcount.Count); // Active, On Leave, Terminated: one grouped query, zero for missing statuses
        Assert.True(headcount[0] >= 1 && headcount[2] == 0, string.Join(",", headcount));
        var forecast = await Ok(await x.C.GetAsync($"{Fin}/reports/cash-forecast?weeks=8&legalEntityId={x.Entity}"));
        Assert.Contains(forecast["sources"]!.AsArray(), s => (string?)s!["key"] == "payroll");

        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail); // the general workspace has no HR module
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{Hr}/employees")).StatusCode);
        Assert.DoesNotContain((await Ok(await admin.GetAsync("/api/v1/dashboard")))["kpis"]!.AsArray(), k => (string?)k!["key"] == "employees");
    }

    [Fact]
    public async Task Pay_amounts_are_money_and_an_adjustment_cannot_make_pay_negative()
    {
        var x = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Odd", 1_000.123m))).StatusCode);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Small", 1_000m)), HttpStatusCode.Created);
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 4 }), HttpStatusCode.Created);
        var line = (string)run["lines"]![0]!["id"]!;
        var runId = (string)run["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Hr}/payroll/{runId}/lines/{line}", new { adjustment = 10.005m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Hr}/payroll/{runId}/lines/{line}", new { adjustment = -5_000m })).StatusCode);
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/payroll/{runId}/lines/{line}", new { adjustment = -200m, note = "Fine" }));
    }

    [Fact]
    public async Task Paying_a_payroll_needs_a_real_date_not_before_its_month()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Payee", 5_000m)), HttpStatusCode.Created);
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 5 }), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "0001-01-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-04-30" })).StatusCode);
        Assert.Equal("paid", (string)(await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-05-28" })))["status"]!);
    }

    [Fact]
    public async Task Changing_or_deleting_a_payroll_withdraws_its_pending_approval()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Approval Edit", 5_000m)), HttpStatusCode.Created);
        async Task<string> StatusAsync(string requestId)
            => ((string)(await Ok(await x.C.GetAsync("/api/v1/approvals?scope=mine"))).AsArray().Single(a => (string)a!["id"]! == requestId)!["status"]!).ToLowerInvariant();
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.payrollRequiresApproval"] = true })).StatusCode);
        try
        {
            var first = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 6 }), HttpStatusCode.Created);
            var id = (string)first["id"]!;
            var pending = await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
            var requestId = (string)pending["approvalRequestId"]!;
            Assert.Equal("pending", await StatusAsync(requestId));
            await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/recalculate", null));
            Assert.Equal("withdrawn", await StatusAsync(requestId));

            var again = await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
            var secondRequest = (string)again["approvalRequestId"]!;
            Assert.NotEqual(requestId, secondRequest);
            Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Hr}/payroll/{id}")).StatusCode);
            Assert.Equal("withdrawn", await StatusAsync(secondRequest));
        }
        finally { await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.payrollRequiresApproval"] = false }); }
    }

    [Fact]
    public async Task Attendance_cannot_change_after_the_payroll_is_posted_or_for_a_month_the_employee_was_not_employed()
    {
        var x = await SetupAsync();
        var emp = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Attend", 4_000m, join: "2026-03-10")), HttpStatusCode.Created);
        var employeeId = (string)emp["id"]!;
        object Att(int month) => new { employeeId, year = 2026, month, workingDays = 26, daysPresent = 24, unpaidAbsentDays = 2, paidLeaveDays = 0, overtimeHours = 0 };
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Hr}/attendance", Att(2))).StatusCode); // joined in March
        var saved = await Ok(await x.C.PutAsJsonAsync($"{Hr}/attendance", Att(7)));
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 7 }), HttpStatusCode.Created);
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/attendance", Att(7))); // a draft run does not lock attendance
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Hr}/attendance", Att(7))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.DeleteAsync($"{Hr}/attendance/{(string)saved["recordId"]!}")).StatusCode);
    }

    [Fact]
    public async Task An_iban_must_look_like_one_and_is_stored_upper_case()
    {
        var x = await SetupAsync();
        object WithIban(string? iban) => new { legalEntityId = x.Entity, fullName = "Iban " + Guid.NewGuid().ToString("N")[..5], designation = "Agent", joinDate = "2020-01-01", status = "active", basicSalary = 1_000m, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m, iban };
        foreach (var bad in new[] { "12345", "AE07 0331 2345 6789 0123 456", "AE0703312", "AEXX0331234567890123456" })
            Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/employees", WithIban(bad))).StatusCode);
        var ok = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", WithIban("ae070331234567890123456")), HttpStatusCode.Created);
        Assert.Equal("AE070331234567890123456", (string)ok["iban"]!);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", WithIban(null)), HttpStatusCode.Created);
    }
}
