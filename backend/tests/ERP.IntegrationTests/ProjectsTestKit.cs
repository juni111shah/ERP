using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Shared setup for the Projects rule tests: a legal entity, a customer and helpers that create projects, tasks, milestones and timesheets.</summary>
internal sealed class ProjectsTestKit(HttpClient c, string entity, string customer)
{
    public const string Prj = "/api/v1/m/projects";
    public HttpClient C => c;
    public string Entity => entity;
    public string Customer => customer;

    public static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    public static async Task<ProjectsTestKit> NewAsync(ApiFactory f)
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/projects/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "PrjRules " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        return new ProjectsTestKit(c, entity, customer);
    }

    public async Task<string> ProjectAsync(string status = "Active", string billingType = "TimeAndMaterials", string? customerId = null)
        => (string)(await Ok(await c.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = entity, name = "P " + Guid.NewGuid().ToString("N")[..6], status, billingType, budgetAmount = 1000m, customerId = customerId ?? customer }), HttpStatusCode.Created))["id"]!;

    public Task<HttpResponseMessage> PostMilestone(string projectId, string status = "Pending", string? achievedDate = null, decimal amount = 100m)
        => c.PostAsJsonAsync($"{Prj}/milestones", new { projectId, title = "M " + Guid.NewGuid().ToString("N")[..4], dueDate = "2026-12-01", amount, status, achievedDate });

    public async Task<string> MilestoneAsync(string projectId, string status = "Pending", decimal amount = 100m)
        => (string)(await Ok(await PostMilestone(projectId, status, status == "Pending" ? null : "2026-06-01", amount), HttpStatusCode.Created))["id"]!;

    public Task<HttpResponseMessage> PostTask(string projectId, string? milestoneId = null, string status = "Todo")
        => c.PostAsJsonAsync($"{Prj}/tasks", new { projectId, milestoneId, title = "T " + Guid.NewGuid().ToString("N")[..4], status, priority = "Medium", estimatedHours = 1m, actualHours = 0m });

    public async Task<string> TaskAsync(string projectId)
        => (string)(await Ok(await PostTask(projectId), HttpStatusCode.Created))["id"]!;

    public Task<HttpResponseMessage> PostTimesheet(string projectId, string? taskId = null, decimal hours = 2m, decimal rate = 50m, bool billable = true)
        => c.PostAsJsonAsync($"{Prj}/timesheets", new { projectId, taskId, date = "2026-06-01", workerName = "Ann", hours, hourlyRate = rate, description = "Work", isBillable = billable });
}
