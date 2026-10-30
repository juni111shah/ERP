using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-051: timesheets reference their own project's tasks, stay off closed projects and carry 2-decimal money.</summary>
public class ProjectTimesheetRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_timesheet_task_must_belong_to_the_project()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var a = await k.ProjectAsync();
        var b = await k.ProjectAsync();
        var foreignTask = await k.TaskAsync(b);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostTimesheet(a, foreignTask)).StatusCode);
        var ownTask = await k.TaskAsync(a);
        var ts = await ProjectsTestKit.Ok(await k.PostTimesheet(a, ownTask), HttpStatusCode.Created);
        var update = await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/timesheets/{(string)ts["id"]!}", new { projectId = a, taskId = foreignTask, date = "2026-06-01", workerName = "Ann", hours = 1m, hourlyRate = 10m, isBillable = true });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Closed_projects_take_no_time_and_amounts_round_to_two_decimals()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        var ts = await ProjectsTestKit.Ok(await k.PostTimesheet(p, null, hours: 1.333m, rate: 10.005m), HttpStatusCode.Created);
        Assert.Equal(13.34m, (decimal)ts["totalAmount"]!); // 13.33665 -> 2 decimals

        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{p}/status", new { status = "Cancelled" }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.PostTimesheet(p)).StatusCode);
    }

    [Fact]
    public async Task Time_on_a_non_billable_project_is_never_billable()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync(billingType: "NonBillable");
        var ts = await ProjectsTestKit.Ok(await k.PostTimesheet(p, billable: true), HttpStatusCode.Created);
        Assert.False((bool)ts["isBillable"]!);
    }
}
