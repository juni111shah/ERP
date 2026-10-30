using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-050: tasks stay inside their project and follow the status flow.</summary>
public class ProjectTaskRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_task_milestone_must_belong_to_the_same_project()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var a = await k.ProjectAsync();
        var b = await k.ProjectAsync();
        var foreign = await k.MilestoneAsync(b);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostTask(a, foreign)).StatusCode);

        var own = await k.MilestoneAsync(a);
        var task = await ProjectsTestKit.Ok(await k.PostTask(a, own), HttpStatusCode.Created);
        var update = await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/tasks/{(string)task["id"]!}", new { projectId = a, milestoneId = foreign, title = "T", status = "Todo", priority = "Medium", estimatedHours = 1m, actualHours = 0m });
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Editing_a_task_cannot_skip_the_status_flow_and_closed_projects_take_no_new_tasks()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        var id = await k.TaskAsync(p);
        object Body(string status) => new { projectId = p, title = "T", status, priority = "Medium", estimatedHours = 1m, actualHours = 0m };
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/tasks/{id}", Body("Done"))).StatusCode); // Todo -> Done is not allowed
        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/tasks/{id}", Body("InProgress")));
        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/tasks/{id}", Body("InProgress"))); // same status is fine

        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{p}/status", new { status = "Completed" }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.PostTask(p)).StatusCode);
    }
}
