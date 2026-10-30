using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

public class ProjectsBillingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Prj = "/api/v1/m/projects";

    private async Task<HttpClient> AdminAsync() => (await f.SignInAsync(ApiFactory.AdminEmail)).Client;

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Setup(HttpClient C, string Entity, string Customer, string TaxId);

    private async Task<Setup> NewAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/projects/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "PrjLegal " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = true }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", paymentTermsDays = 15 }), HttpStatusCode.Created))["id"]!;
        var tax = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "TX" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5", kind = "vat", rate = 5, isDefault = true, legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        return new Setup(c, entity, customer, tax);
    }

    [Fact]
    public async Task Billing_project_without_customer_returns_400_and_flags_nothing()
    {
        var s = await NewAsync();

        // 1. Create project with no customer
        var projectRes = await Ok(await s.C.PostAsJsonAsync($"{Prj}/projects", new
        {
            legalEntityId = s.Entity,
            name = "Internal R&D Project",
            status = "Active",
            billingType = "TimeAndMaterials",
            budgetAmount = 50000m
        }), HttpStatusCode.Created);
        var projectId = (string)projectRes["id"]!;

        // 2. Add billable timesheet
        var tsRes = await Ok(await s.C.PostAsJsonAsync($"{Prj}/timesheets", new
        {
            projectId,
            date = "2026-06-01",
            workerName = "Alice Dev",
            hours = 8m,
            hourlyRate = 120m,
            description = "Architecture Design",
            isBillable = true
        }), HttpStatusCode.Created);
        var timesheetId = (string)tsRes["id"]!;

        // 3. Attempt to bill the project -> must return 400 Bad Request
        var billRes = await s.C.PostAsJsonAsync($"{Prj}/billing/bill", new
        {
            projectId
        });
        Assert.Equal(HttpStatusCode.BadRequest, billRes.StatusCode);

        // 4. Verify timesheet is unflagged and has no invoice id
        var tsCheck = await Ok(await s.C.GetAsync($"{Prj}/timesheets/{timesheetId}"));
        Assert.False((bool)tsCheck["isBilled"]!);
        Assert.Null(tsCheck["invoiceId"]);
    }

    [Fact]
    public async Task Billing_project_with_customer_creates_invoice_with_terms_and_flags_timesheets_with_invoice_id()
    {
        var s = await NewAsync();

        // 1. Create project with customer
        var projectRes = await Ok(await s.C.PostAsJsonAsync($"{Prj}/projects", new
        {
            legalEntityId = s.Entity,
            name = "Client Delivery Alpha",
            customerId = s.Customer,
            customerName = "Client Alpha",
            status = "Active",
            billingType = "TimeAndMaterials",
            budgetAmount = 80000m
        }), HttpStatusCode.Created);
        var projectId = (string)projectRes["id"]!;

        // 2. Create billable timesheet
        var tsRes = await Ok(await s.C.PostAsJsonAsync($"{Prj}/timesheets", new
        {
            projectId,
            date = "2026-06-01",
            workerName = "Bob Engineer",
            hours = 10m,
            hourlyRate = 150m,
            description = "Core Module Implementation",
            isBillable = true
        }), HttpStatusCode.Created);
        var timesheetId = (string)tsRes["id"]!;

        // 3. Create milestone and achieve it
        var msRes = await Ok(await s.C.PostAsJsonAsync($"{Prj}/milestones", new
        {
            projectId,
            title = "Phase 1 Delivery",
            dueDate = "2026-06-15",
            amount = 3000m
        }), HttpStatusCode.Created);
        var milestoneId = (string)msRes["id"]!;

        await Ok(await s.C.PostAsJsonAsync($"{Prj}/milestones/{milestoneId}/achieve", new { achievedDate = "2026-06-10" }));

        // 4. Bill the project
        var billRes = await s.C.PostAsJsonAsync($"{Prj}/billing/bill", new
        {
            projectId
        });
        var billResult = await Ok(billRes);
        var invoiceId = (string)billResult["invoiceId"]!;
        Assert.NotNull(invoiceId);

        // 5. Inspect generated invoice
        var invoice = await Ok(await s.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        var invDate = DateOnly.Parse((string)invoice["invoiceDate"]!);
        var dueDate = DateOnly.Parse((string)invoice["dueDate"]!);

        // Due date uses customer payment terms (15 days)
        Assert.Equal(invDate.AddDays(15), dueDate);

        // 6. Verify timesheet is flagged with InvoiceId
        var tsCheck = await Ok(await s.C.GetAsync($"{Prj}/timesheets/{timesheetId}"));
        Assert.True((bool)tsCheck["isBilled"]!);
        Assert.Equal(invoiceId, (string)tsCheck["invoiceId"]!);

        // 7. Verify milestone is Invoiced
        var msCheck = await Ok(await s.C.GetAsync($"{Prj}/milestones/{milestoneId}"));
        Assert.Equal("invoiced", (string)msCheck["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Billing_overview_totals_unbilled_time_per_project_and_hides_idle_inactive_projects()
    {
        var s = await NewAsync();
        async Task<string> NewProject(string name, string status) => (string)(await Ok(await s.C.PostAsJsonAsync($"{Prj}/projects", new { legalEntityId = s.Entity, customerId = s.Customer, name, status, billingType = "TimeAndMaterials", budgetAmount = 1000m }), HttpStatusCode.Created))["id"]!;
        var busy = await NewProject("Busy " + Guid.NewGuid().ToString("N")[..5], "Active");
        var idle = await NewProject("Idle " + Guid.NewGuid().ToString("N")[..5], "OnHold");
        foreach (var hours in new[] { 8m, 2m })
            await Ok(await s.C.PostAsJsonAsync($"{Prj}/timesheets", new { projectId = busy, date = "2026-06-01", workerName = "Alice", hours, hourlyRate = 100m, description = "Work", isBillable = true }), HttpStatusCode.Created);

        var overview = (await Ok(await s.C.GetAsync($"{Prj}/billing/overview"))).AsArray();
        var row = overview.Single(r => (string)r!["projectId"]! == busy)!;
        Assert.Equal((10m, 1000m, 1000m), ((decimal)row["unbilledHours"]!, (decimal)row["unbilledHoursAmount"]!, (decimal)row["totalUnbilledAmount"]!));
        Assert.DoesNotContain(overview, r => (string)r!["projectId"]! == idle);
    }
}
