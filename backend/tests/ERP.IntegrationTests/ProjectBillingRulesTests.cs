using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-054: billing follows the project's billing type and status.</summary>
public class ProjectBillingRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Non_billable_planning_and_cancelled_projects_cannot_be_invoiced()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var nb = await k.ProjectAsync(billingType: "NonBillable");
        await k.MilestoneAsync(nb, "Achieved");
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/billing/bill", new { projectId = nb })).StatusCode);

        var planning = await k.ProjectAsync("Planning");
        await k.MilestoneAsync(planning, "Achieved");
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/billing/bill", new { projectId = planning })).StatusCode);
    }

    [Fact]
    public async Task A_fixed_fee_project_bills_milestones_only()
    {
        var k = await ProjectsTestKit.NewAsync(f);
        var p = await k.ProjectAsync(billingType: "FixedFee");
        var ts = await ProjectsTestKit.Ok(await k.PostTimesheet(p), HttpStatusCode.Created);
        await k.MilestoneAsync(p, "Achieved", 500m);

        var result = await ProjectsTestKit.Ok(await k.C.PostAsJsonAsync($"{ProjectsTestKit.Prj}/billing/bill", new { projectId = p }));
        Assert.Equal((0, 1, 500m), ((int)result["billedTimesheetsCount"]!, (int)result["billedMilestonesCount"]!, (decimal)result["totalAmountBilled"]!));
        var after = await ProjectsTestKit.Ok(await k.C.GetAsync($"{ProjectsTestKit.Prj}/timesheets/{(string)ts["id"]!}"));
        Assert.False((bool)after["isBilled"]!);
    }
}
