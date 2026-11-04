using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;
using static ERP.IntegrationTests.HrKit;

namespace ERP.IntegrationTests;

/// <summary>Self-service: "my" record, leave, balances and payslips for a user linked to an employee.</summary>
public class HrSelfServiceTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_linked_user_files_submits_and_cancels_their_own_leave_and_reads_balances_without_any_hr_permission()
    {
        var k = await StartAsync(f);
        var annual = (string)(await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/leave-types", new { code = Unique("AN"), name = "Annual", isPaid = true, annualDays = 10m, carryForwardCap = 0m, requiresApproval = true }), HttpStatusCode.Created))["id"]!;
        var (me, meUser) = await k.UserAsync("Me", CorePermissions.AttachmentsRead);   // no hr.* permission at all
        var (other, otherUser) = await k.UserAsync("Other", CorePermissions.AttachmentsRead);
        var mine = await k.EmployeeAsync("My Self", 4_000m, b => b["userId"] = meUser);
        var theirs = await k.EmployeeAsync("Their Self", 6_000m, b => b["userId"] = otherUser);

        // own record, including own pay
        var record = await Ok(await me.GetAsync($"{Hr}/me"));
        Assert.Equal(mine, (string)record["id"]!);
        Assert.Equal(4_000m, (decimal)record["basicSalary"]!);

        // create a draft: the employee comes from the link even when the body names somebody else
        var draft = await Ok(await me.PostAsJsonAsync($"{Hr}/me/leave", new { employeeId = theirs, leaveTypeId = annual, fromDate = "2032-04-05", toDate = "2032-04-09", reason = "Trip" }), HttpStatusCode.Created);
        Assert.Equal(mine, (string)draft["employeeId"]!);
        Assert.Equal("draft", (string)draft["status"]!);
        var id = (string)draft["id"]!;
        var submitted = await Ok(await me.PostAsync($"{Hr}/me/leave/{id}/submit", null));
        Assert.Equal("submitted", (string)submitted["status"]!);

        var balance = (await Ok(await me.GetAsync($"{Hr}/me/balances?year=2032"))).AsArray().Single(r => (string?)r!["leaveTypeId"] == annual)!;
        Assert.Equal(mine, (string)balance["employeeId"]!);
        Assert.Equal(5m, (decimal)balance["pending"]!);

        var listed = (await Ok(await me.GetAsync($"{Hr}/me/leave"))).AsArray();
        Assert.Equal(id, (string)listed.Single()!["id"]!);

        // somebody else's request is invisible: list, submit and cancel
        var theirDraft = (string)(await Ok(await other.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = annual, fromDate = "2032-06-07", toDate = "2032-06-08" }), HttpStatusCode.Created))["id"]!;
        Assert.DoesNotContain((await Ok(await me.GetAsync($"{Hr}/me/leave"))).AsArray(), r => (string?)r!["id"] == theirDraft);
        Assert.Equal(HttpStatusCode.NotFound, (await me.PostAsync($"{Hr}/me/leave/{theirDraft}/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await me.PostAsync($"{Hr}/me/leave/{theirDraft}/cancel", null)).StatusCode);

        var cancelled = await Ok(await me.PostAsync($"{Hr}/me/leave/{id}/cancel", null));
        Assert.Equal("cancelled", (string)cancelled["status"]!);
        Assert.Equal(0m, (decimal)(await Ok(await me.GetAsync($"{Hr}/me/balances?year=2032"))).AsArray().Single(r => (string?)r!["leaveTypeId"] == annual)!["pending"]!);

        // bad input is a validation error
        Assert.Equal(HttpStatusCode.BadRequest, (await me.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = annual, fromDate = "2032-04-09", toDate = "2032-04-05" })).StatusCode);
    }

    [Fact]
    public async Task Payslips_show_only_the_own_lines_of_posted_and_paid_runs()
    {
        var k = await StartAsync(f);
        var (me, meUser) = await k.UserAsync("Slip", CorePermissions.AttachmentsRead);
        var (other, otherUser) = await k.UserAsync("NoSlip", CorePermissions.AttachmentsRead);
        await k.EmployeeAsync("Slip Owner", 4_000m, b => b["userId"] = meUser);
        await k.EmployeeAsync("Slip Other", 7_000m, b => b["userId"] = otherUser);

        var draft = (string)(await Ok(await k.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = k.Entity, year = 2032, month = 7 }), HttpStatusCode.Created))["id"]!;
        Assert.Empty((await Ok(await me.GetAsync($"{Hr}/me/payslips"))).AsArray()); // a draft run is not a payslip yet

        await Ok(await k.Owner.PostAsync($"{Hr}/payroll/{draft}/post", null));
        var slips = (await Ok(await me.GetAsync($"{Hr}/me/payslips"))).AsArray();
        var slip = slips.Single()!;
        Assert.Equal("posted", (string)slip["status"]!);
        Assert.Equal(7, (int)slip["month"]!);
        Assert.Equal(4_700m, (decimal)slip["grossEarnings"]!); // 4,000 basic + 500 housing + 200 transport
        Assert.Equal(100m, (decimal)slip["deductions"]!);
        Assert.Equal(4_600m, (decimal)slip["netPay"]!);
        Assert.StartsWith("PR", (string)slip["runNumber"]!);

        var lineId = (string)slip["lineId"]!;
        Assert.Equal(4_000m, (decimal)(await Ok(await me.GetAsync($"{Hr}/me/payslips/{lineId}")))["basic"]!);
        // the other person cannot open it by id
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Hr}/me/payslips/{lineId}")).StatusCode);
        Assert.Equal(7_000m, (decimal)(await Ok(await other.GetAsync($"{Hr}/me/payslips"))).AsArray().Single()!["basic"]!);
    }

    [Fact]
    public async Task A_user_with_no_linked_employee_gets_404_on_every_self_service_endpoint()
    {
        var k = await StartAsync(f);
        var (stranger, _) = await k.UserAsync("Stranger", CorePermissions.AttachmentsRead);
        foreach (var path in new[] { "me", "me/leave", "me/balances", "me/payslips", "me/team/leave" })
        {
            var err = await ErrorOf(await stranger.GetAsync($"{Hr}/{path}"), HttpStatusCode.NotFound);
            Assert.Contains("No employee is linked to your account.", err.ToJsonString());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"{Hr}/me/leave", new { leaveTypeId = Guid.NewGuid(), fromDate = "2032-04-05", toDate = "2032-04-06" })).StatusCode);
    }

    [Fact]
    public async Task Filing_leave_for_another_employee_needs_hr_leave_manage()
    {
        var k = await StartAsync(f);
        var type = await k.LeaveTypeAsync();
        var (staff, staffUser) = await k.UserAsync("Filer", Perm.LeaveRead, Perm.LeaveRequest);
        var (hr, _) = await k.UserAsync("HrDesk", Perm.LeaveRead, Perm.LeaveRequest, Perm.LeaveManage);
        var own = await k.EmployeeAsync("Filer Self", tweak: b => b["userId"] = staffUser);
        var someone = await k.EmployeeAsync("Somebody Else");

        object Body(string employee, string from, string to) => new { employeeId = employee, leaveTypeId = type, fromDate = from, toDate = to };
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"{Hr}/leave", Body(someone, "2032-08-02", "2032-08-03"))).StatusCode);
        var mine = await Ok(await staff.PostAsJsonAsync($"{Hr}/leave", Body(own, "2032-08-02", "2032-08-03")), HttpStatusCode.Created);

        // updating a draft: staff cannot repoint it, HR can file for anyone
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PutAsJsonAsync($"{Hr}/leave/{(string)mine["id"]!}", Body(someone, "2032-08-02", "2032-08-03"))).StatusCode);
        await Ok(await hr.PostAsJsonAsync($"{Hr}/leave", Body(someone, "2032-08-09", "2032-08-10")), HttpStatusCode.Created);
    }
}
