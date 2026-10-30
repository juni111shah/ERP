using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-053: milestone edits that used to 409 now work, and invoiced or future-dated milestones are protected.</summary>
public class ProjectMilestoneRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_pending_milestone_can_be_edited_without_changing_its_status()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        var id = await k.MilestoneAsync(p);
        var res = await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/milestones/{id}", new { projectId = p, title = "Renamed", dueDate = "2026-12-15", amount = 250m, status = "Pending" }));
        Assert.Equal(("Renamed", 250m), ((string)res["title"]!, (decimal)res["amount"]!));
        Assert.Null(res["achievedDate"]);
    }

    [Fact]
    public async Task An_invoiced_milestone_cannot_be_edited_or_deleted_and_cannot_be_created_invoiced()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, "Invoiced", "2026-06-01")).StatusCode);

        var id = await k.MilestoneAsync(p, "Achieved");
        await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/billing/bill", new { projectId = p }));
        var edit = await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/milestones/{id}", new { projectId = p, title = "Cheaper", dueDate = "2026-12-15", amount = 1m, status = "Invoiced" });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{ProjectsTestKit.Prj}/milestones/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_achieved_date_cannot_be_in_the_future()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await k.PostMilestone(p, "Achieved", "2099-01-01")).StatusCode);
        var id = await k.MilestoneAsync(p);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/milestones/{id}/achieve", new { achievedDate = "2099-01-01" })).StatusCode);
        await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/milestones/{id}/achieve", new { achievedDate = "2026-06-02" }));
    }
}
