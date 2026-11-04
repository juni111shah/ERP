using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;
using static ERP.IntegrationTests.HrKit;

namespace ERP.IntegrationTests;

/// <summary>HR access: salary permission, segregation of duties, manager approval, structure, safe delete, on-leave flag.</summary>
public class HrAccessTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static string Today(int offset = 0) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset).ToString("yyyy-MM-dd");

    [Fact]
    public async Task Pay_fields_are_null_without_hr_salary_read_in_the_detail_and_the_list()
    {
        var k = await StartAsync(f);
        var id = await k.EmployeeAsync("Pay Hidden", 4_000m);
        var (reader, _) = await k.UserAsync("Reader", Perm.EmployeesRead);
        var (payReader, _) = await k.UserAsync("PayReader", Perm.EmployeesRead, Perm.SalaryRead);
        var (manager, _) = await k.UserAsync("Manager", Perm.EmployeesRead, Perm.EmployeesManage);

        var detail = await Ok(await reader.GetAsync($"{Hr}/employees/{id}"));
        foreach (var field in new[] { "basicSalary", "housingAllowance", "transportAllowance", "otherAllowances", "recurringDeductions", "iban", "grossMonthly", "gratuityAccrued" })
            Assert.Null(detail[field]);
        Assert.Equal("Pay Hidden", (string)detail["fullName"]!);

        var row = (await Ok(await reader.GetAsync($"{Hr}/employees?search=Pay Hidden"))).AsArray().Single()!;
        foreach (var field in new[] { "basicSalary", "housingAllowance", "transportAllowance", "otherAllowances", "grossMonthly" }) Assert.Null(row[field]);

        // with the permission the values are as before
        var shown = await Ok(await payReader.GetAsync($"{Hr}/employees/{id}"));
        Assert.Equal(4_000m, (decimal)shown["basicSalary"]!);
        Assert.Equal(4_700m, (decimal)shown["grossMonthly"]!);
        Assert.Equal(4_700m, (decimal)(await Ok(await payReader.GetAsync($"{Hr}/employees?search=Pay Hidden"))).AsArray().Single()!["grossMonthly"]!);

        // sorting or filtering by a pay column is refused for everyone
        Assert.Equal(HttpStatusCode.BadRequest, (await payReader.GetAsync($"{Hr}/employees?sort=basicSalary")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync($"{Hr}/employees?filter=basicSalary:gt:1")).StatusCode);

        // a manager without the permission edits the person but cannot change (or wipe) the pay
        var edit = k.EmployeeBody("Pay Hidden Renamed", 1m, b => { b["housingAllowance"] = null; b["basicSalary"] = 1m; b["iban"] = null; });
        await Ok(await manager.PutAsJsonAsync($"{Hr}/employees/{id}", edit));
        var after = await Ok(await k.Owner.GetAsync($"{Hr}/employees/{id}"));
        Assert.Equal("Pay Hidden Renamed", (string)after["fullName"]!);
        Assert.Equal(4_000m, (decimal)after["basicSalary"]!);
        Assert.Equal(500m, (decimal)after["housingAllowance"]!);
        Assert.NotNull(after["iban"]);
    }

    [Fact]
    public async Task Nobody_decides_their_own_leave_request_unless_the_setting_is_off()
    {
        var k = await StartAsync(f);
        var type = await k.LeaveTypeAsync();
        var (staff, staffUser) = await k.UserAsync("Staff", Perm.LeaveRead, Perm.LeaveRequest, Perm.LeaveApprove);
        var (approver, _) = await k.UserAsync("Approver", Perm.LeaveRead, Perm.LeaveApprove);
        await k.EmployeeAsync("Staff Member", tweak: b => b["userId"] = staffUser);
        await k.SetAsync("hr.blockSelfApproval", true);

        async Task<string> FileAsync(string from, string to)
            => (string)(await Ok(await staff.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = type, fromDate = from, toDate = to, submit = true }), HttpStatusCode.Created))["id"]!;

        var first = await FileAsync("2032-03-01", "2032-03-02");
        var refused = await ErrorOf(await staff.PostAsJsonAsync($"{Hr}/leave/{first}/approve", new { }), HttpStatusCode.Conflict);
        Assert.Contains("cannot decide your own leave request", refused.ToJsonString());
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"{Hr}/leave/{first}/reject", new { })).StatusCode);

        var approved = await Ok(await approver.PostAsJsonAsync($"{Hr}/leave/{first}/approve", new { comment = "ok" }));
        Assert.Equal("approved", (string)approved["status"]!);

        await k.SetAsync("hr.blockSelfApproval", false);
        try
        {
            var second = await FileAsync("2032-03-08", "2032-03-09");
            Assert.Equal("approved", (string)(await Ok(await staff.PostAsJsonAsync($"{Hr}/leave/{second}/approve", new { })))["status"]!);
        }
        finally { await k.SetAsync("hr.blockSelfApproval", true); }
    }

    [Fact]
    public async Task A_manager_can_decide_a_direct_reports_request_without_the_approve_permission()
    {
        var k = await StartAsync(f);
        var type = await k.LeaveTypeAsync();
        var (boss, bossUser) = await k.UserAsync("Boss", Perm.LeaveRead);        // no hr.leave.approve
        var (staff, staffUser) = await k.UserAsync("Report", Perm.LeaveRequest);
        var (other, otherUser) = await k.UserAsync("Other", Perm.LeaveRead);     // not the manager, no approve permission
        var bossEmployee = await k.EmployeeAsync("Boss Person", tweak: b => b["userId"] = bossUser);
        await k.EmployeeAsync("Report Person", tweak: b => { b["userId"] = staffUser; b["managerEmployeeId"] = bossEmployee; });
        await k.EmployeeAsync("Other Person", tweak: b => b["userId"] = otherUser);

        var request = (string)(await Ok(await staff.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = type, fromDate = "2032-05-03", toDate = "2032-05-04", submit = true }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"{Hr}/leave/{request}/approve", new { })).StatusCode);

        var team = (await Ok(await boss.GetAsync($"{Hr}/me/team/leave?status=submitted"))).AsArray();
        Assert.Contains(team, r => (string?)r!["id"] == request);
        Assert.Empty((await Ok(await other.GetAsync($"{Hr}/me/team/leave"))).AsArray());

        var decided = await Ok(await boss.PostAsJsonAsync($"{Hr}/leave/{request}/approve", new { comment = "Fine" }));
        Assert.Equal("approved", (string)decided["status"]!);
    }

    [Fact]
    public async Task Payroll_separation_of_duties_needs_a_second_person_to_post_and_a_third_to_pay()
    {
        var k = await StartAsync(f);
        await k.EmployeeAsync("Payroll Staff", 5_000m);
        var bank = (string)(await Ok(await k.Owner.GetAsync("/api/v1/finance/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var (poster, _) = await k.UserAsync("Poster", Perm.PayrollRead, Perm.PayrollPost, Perm.PayrollPay);
        var (payer, _) = await k.UserAsync("Payer", Perm.PayrollRead, Perm.PayrollPay, CorePermissions.BankRead);
        await k.SetAsync("hr.payrollSeparationOfDuties", true);
        try
        {
            var run = (string)(await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = k.Entity, year = 2027, month = 1 }), HttpStatusCode.Created))["id"]!;
            // the owner generated the run, so the owner cannot post it
            var refused = await ErrorOf(await k.Owner.PostAsync($"{Hr}/payroll/{run}/post", null), HttpStatusCode.Conflict);
            Assert.Contains("another user must post", refused.ToJsonString());

            Assert.Equal("posted", (string)(await Ok(await poster.PostAsync($"{Hr}/payroll/{run}/post", null)))["status"]!);
            // the poster cannot pay what they posted; somebody else can
            var payBody = new { bankAccountId = bank, date = "2027-01-31" };
            Assert.Contains("another user must pay", (await ErrorOf(await poster.PostAsJsonAsync($"{Hr}/payroll/{run}/pay", payBody), HttpStatusCode.Conflict)).ToJsonString());
            Assert.Equal("paid", (string)(await Ok(await payer.PostAsJsonAsync($"{Hr}/payroll/{run}/pay", payBody)))["status"]!);

            // with the rule off one person may do everything
            await k.SetAsync("hr.payrollSeparationOfDuties", false);
            var run2 = (string)(await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = k.Entity, year = 2027, month = 2 }), HttpStatusCode.Created))["id"]!;
            Assert.Equal("posted", (string)(await Ok(await k.Owner.PostAsync($"{Hr}/payroll/{run2}/post", null)))["status"]!);
        }
        finally { await k.SetAsync("hr.payrollSeparationOfDuties", false); }
    }

    [Fact]
    public async Task Departments_and_positions_fill_the_employee_and_cannot_be_deleted_while_used()
    {
        var k = await StartAsync(f);
        var dept = await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/departments", new { code = Unique("D"), name = "Operations" }), HttpStatusCode.Created);
        var child = await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/departments", new { code = Unique("D"), name = "Field Ops", parentDepartmentId = (string)dept["id"]! }), HttpStatusCode.Created);
        var pos = await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/positions", new { code = Unique("P"), title = "Site Supervisor", grade = "G4" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.PostAsJsonAsync($"{Hr}/departments", new { code = (string)dept["code"]!, name = "Dup" })).StatusCode);

        var id = await k.EmployeeAsync("Structured", tweak: b =>
        {
            b["departmentId"] = (string)child["id"]!; b["positionId"] = (string)pos["id"]!; b["designation"] = null;
            b["dateOfBirth"] = "1990-02-03"; b["gender"] = "Female"; b["maritalStatus"] = "Single"; b["emergencyContactName"] = "Sam"; b["emergencyContactPhone"] = "0500000000";
            b["address"] = "1 Main Street"; b["contractType"] = "fixedTerm"; b["probationEndDate"] = "2020-04-01"; b["contractEndDate"] = "2030-01-01"; b["noticePeriodDays"] = 30; b["bankName"] = "Test Bank";
        });
        var e = await Ok(await k.Owner.GetAsync($"{Hr}/employees/{id}"));
        Assert.Equal("Field Ops", (string)e["department"]!);
        Assert.Equal("Site Supervisor", (string)e["designation"]!);
        Assert.Equal("fixedTerm", (string)e["contractType"]!);
        Assert.Equal(30, (int)e["noticePeriodDays"]!);
        Assert.Equal("Test Bank", (string)e["bankName"]!);

        var byDept = (await Ok(await k.Owner.GetAsync($"{Hr}/employees?filter=departmentId:eq:{(string)child["id"]!}"))).AsArray();
        Assert.Equal(id, (string)byDept.Single()!["id"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.DeleteAsync($"{Hr}/departments/{(string)child["id"]!}")).StatusCode); // used by an employee
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.DeleteAsync($"{Hr}/departments/{(string)dept["id"]!}")).StatusCode);  // has a sub-department
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.DeleteAsync($"{Hr}/positions/{(string)pos["id"]!}")).StatusCode);
        // a parent loop is refused
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.PutAsJsonAsync($"{Hr}/departments/{(string)dept["id"]!}", new { code = (string)dept["code"]!, name = "Operations", parentDepartmentId = (string)child["id"]! })).StatusCode);

        // contract and probation end must be after the join date
        Assert.Equal(HttpStatusCode.BadRequest, (await k.Owner.PostAsJsonAsync($"{Hr}/employees", k.EmployeeBody("Bad contract", tweak: b => b["contractEndDate"] = "2019-12-31"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.Owner.PostAsJsonAsync($"{Hr}/employees", k.EmployeeBody("Bad probation", tweak: b => b["probationEndDate"] = "2020-01-01"))).StatusCode);

        // free the department and position; then they can go
        await Ok(await k.Owner.PutAsJsonAsync($"{Hr}/employees/{id}", k.EmployeeBody("Structured", tweak: b => { b["departmentId"] = null; b["positionId"] = null; })));
        Assert.Equal(HttpStatusCode.NoContent, (await k.Owner.DeleteAsync($"{Hr}/departments/{(string)child["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await k.Owner.DeleteAsync($"{Hr}/departments/{(string)dept["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await k.Owner.DeleteAsync($"{Hr}/positions/{(string)pos["id"]!}")).StatusCode);
    }

    [Fact]
    public async Task Manager_links_reject_the_employee_itself_and_cycles_and_a_user_links_to_one_employee_only()
    {
        var k = await StartAsync(f);
        var (_, userId) = await k.UserAsync("Linked", Perm.EmployeesRead);
        var a = await k.EmployeeAsync("Chain A", tweak: b => b["userId"] = userId);
        var b2 = await k.EmployeeAsync("Chain B", tweak: b => b["managerEmployeeId"] = a);
        var c = await k.EmployeeAsync("Chain C", tweak: b => b["managerEmployeeId"] = b2);

        Assert.Equal(HttpStatusCode.BadRequest, (await k.Owner.PutAsJsonAsync($"{Hr}/employees/{a}", k.EmployeeBody("Chain A", tweak: b => { b["managerEmployeeId"] = a; b["userId"] = userId; }))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.PutAsJsonAsync($"{Hr}/employees/{a}", k.EmployeeBody("Chain A", tweak: b => { b["managerEmployeeId"] = c; b["userId"] = userId; }))).StatusCode);

        // the account is taken by A; B and a random id are refused
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.PutAsJsonAsync($"{Hr}/employees/{b2}", k.EmployeeBody("Chain B", tweak: b => { b["managerEmployeeId"] = a; b["userId"] = userId; }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.Owner.PostAsJsonAsync($"{Hr}/employees", k.EmployeeBody("Ghost user", tweak: b => b["userId"] = Guid.NewGuid().ToString()))).StatusCode);

        var detail = await Ok(await k.Owner.GetAsync($"{Hr}/employees/{b2}"));
        Assert.Equal("Chain A", (string)detail["managerName"]!);
        var linked = await Ok(await k.Owner.GetAsync($"{Hr}/employees/{a}"));
        Assert.Equal("User Linked", (string)linked["userName"]!);

        var chart = await Ok(await k.Owner.GetAsync($"{Hr}/org-chart"));
        var root = chart["reportingLines"]!.AsArray().Single(n => (string?)n!["id"] == a)!;
        Assert.Equal(b2, (string)root["reports"]![0]!["id"]!);
        Assert.Equal(c, (string)root["reports"]![0]!["reports"]![0]!["id"]!);
    }

    [Fact]
    public async Task An_employee_with_history_is_terminated_not_deleted_and_on_leave_today_follows_approved_leave()
    {
        var k = await StartAsync(f);
        var type = await k.LeaveTypeAsync();
        var (awayUser, awayUserId) = await k.UserAsync("Away", Perm.LeaveRequest);
        var (waitUser, waitUserId) = await k.UserAsync("Waiting", Perm.LeaveRequest);
        var away = await k.EmployeeAsync("Away Person", tweak: b => b["userId"] = awayUserId);
        var waiting = await k.EmployeeAsync("Waiting Person", tweak: b => b["userId"] = waitUserId);
        var clean = await k.EmployeeAsync("Clean Person");

        var awayLeave = (string)(await Ok(await awayUser.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = type, fromDate = Today(-3), toDate = Today(3), submit = true }), HttpStatusCode.Created))["id"]!;
        await Ok(await waitUser.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = type, fromDate = Today(-3), toDate = Today(3), submit = true }), HttpStatusCode.Created); // submitted, not approved
        async Task<JsonNode> Row(string name) => (await Ok(await k.Owner.GetAsync($"{Hr}/employees?search={Uri.EscapeDataString(name)}"))).AsArray().Single()!;

        Assert.False((bool)(await Row("Away Person"))["onLeaveToday"]!);
        await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/leave/{awayLeave}/approve", new { }));
        Assert.True((bool)(await Row("Away Person"))["onLeaveToday"]!);
        Assert.False((bool)(await Row("Waiting Person"))["onLeaveToday"]!);
        Assert.False((bool)(await Row("Clean Person"))["onLeaveToday"]!);
        Assert.Equal("active", (string)(await Row("Away Person"))["status"]!); // the stored status is untouched
        Assert.True((bool)(await Ok(await k.Owner.GetAsync($"{Hr}/employees/{away}")))["onLeaveToday"]!);

        var refused = await ErrorOf(await k.Owner.DeleteAsync($"{Hr}/employees/{away}"), HttpStatusCode.Conflict);
        Assert.Contains("Terminate the employee instead", refused.ToJsonString());
        Assert.Equal(HttpStatusCode.Conflict, (await k.Owner.DeleteAsync($"{Hr}/employees/{waiting}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await k.Owner.DeleteAsync($"{Hr}/employees/{clean}")).StatusCode);
    }
}
