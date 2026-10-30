using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-052: project status only moves along the allowed flow, customers must be customers, invoiced projects stay.</summary>
public class ProjectLifecycleRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Body(ProjectsTestKit k, string name, string status, string? customer) => new { legalEntityId = k.Entity, name, status, billingType = "TimeAndMaterials", budgetAmount = 10m, customerId = customer };

    [Fact]
    public async Task A_new_project_starts_planning_or_active_and_edits_follow_the_status_flow()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/projects", Body(k, "Done at birth", "Completed", null))).StatusCode);

        var id = await k.ProjectAsync("Planning");
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{id}", Body(k, "X", "Completed", k.Customer))).StatusCode); // Planning -> Completed
        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{id}", Body(k, "Renamed", "Active", k.Customer)));
        await ProjectsTestKit.Ok(await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{id}/status", new { status = "Completed" }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{ProjectsTestKit.Prj}/projects/{id}", Body(k, "Renamed", "Active", k.Customer))).StatusCode); // reopening a completed project by edit
    }

    [Fact]
    public async Task The_project_customer_must_be_a_customer()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var vendor = (string)(await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/projects", Body(k, "V", "Active", vendor))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/projects", Body(k, "V", "Active", Guid.NewGuid().ToString()))).StatusCode);
        await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/projects", Body(k, "Ok", "Active", k.Customer)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_project_with_invoiced_work_cannot_be_deleted()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync();
        await ProjectsTestKit.Ok(await k.PostTimesheet(p), HttpStatusCode.Created);
        await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/billing/bill", new { projectId = p }));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{ProjectsTestKit.Prj}/projects/{p}")).StatusCode);

        var clean = await k.ProjectAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{ProjectsTestKit.Prj}/projects/{clean}")).StatusCode);
    }
}
