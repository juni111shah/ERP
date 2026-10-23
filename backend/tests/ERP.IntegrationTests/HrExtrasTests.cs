using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ERP.IntegrationTests;

/// <summary>HR extras: the UAE WPS SIF file and performance reviews.</summary>
public class HrExtrasTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity);

    private async Task<Ctx> SetupAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Wps " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity);
    }

    private static object Employee(Ctx x, string name, decimal basic, decimal housing = 0m, decimal transport = 0m, decimal deductions = 0m, string? card = null, string? routing = null, string? iban = "AE070331234567890123456")
        => new { legalEntityId = x.Entity, fullName = name, designation = "Agent", department = "Leasing", joinDate = "2020-01-01", status = "active", basicSalary = basic, housingAllowance = housing, transportAllowance = transport, otherAllowances = 0m, recurringDeductions = deductions, iban, labourCardNumber = card, bankRoutingCode = routing };

    [Fact]
    public async Task The_wps_file_has_one_EDR_per_employee_and_a_closing_SCR_and_names_whatever_is_missing()
    {
        var x = await SetupAsync();
        var amir = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Amir Khan", 6_000m, 2_500m, 800m, 500m, card: "12345678901234", routing: "123456789")), HttpStatusCode.Created);
        Assert.Equal("12345678901234", (string)amir["labourCardNumber"]!);
        var neha = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Neha Kapoor", 12_000m)), HttpStatusCode.Created); // no WPS details yet
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = (string)amir["id"]!, year = 2026, month = 10, workingDays = 26, daysPresent = 24, unpaidAbsentDays = 2, paidLeaveDays = 0, overtimeHours = 10 }));
        var run = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var id = (string)run["id"]!;

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.GetAsync($"{Hr}/payroll/{id}/wps-sif")).StatusCode); // still a draft
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));

        var blocked = await x.C.GetAsync($"{Hr}/payroll/{id}/wps-sif");
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var why = await blocked.Content.ReadAsStringAsync();
        Assert.Contains("hr.employerEstablishmentId", why);
        Assert.Contains("hr.employerBankRoutingCode", why);
        Assert.Contains("Neha Kapoor: labour card", why);
        Assert.Contains("Neha Kapoor: bank routing code", why);
        Assert.DoesNotContain("Amir Khan:", why);

        await Ok(await x.C.PutAsJsonAsync($"{Hr}/employees/{(string)neha["id"]!}", Employee(x, "Neha Kapoor", 12_000m, card: "98765432109876", routing: "123456789")));
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.employerEstablishmentId"] = "1234567890123", ["hr.employerBankRoutingCode"] = "987654321" })).StatusCode);

        var res = await x.C.GetAsync($"{Hr}/payroll/{id}/wps-sif");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var lines = (await res.Content.ReadAsStringAsync()).TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        // Amir: gross 9,300 - 620 absence + 312.50 overtime - 500 deductions = 8,492.50 = fixed 8,180.00 + variable 312.50, 2 days unpaid leave
        Assert.Equal("EDR,12345678901234,123456789,AE070331234567890123456,2026-10-01,2026-10-31,31,8180.00,312.50,2", lines[0]);
        Assert.Equal("EDR,98765432109876,123456789,AE070331234567890123456,2026-10-01,2026-10-31,31,12000.00,0.00,0", lines[1]);
        Assert.Matches(new Regex(@"^SCR,1234567890123,987654321,\d{4}-\d{2}-\d{2},\d{4},102026,2,20492\.50,AED$"), lines[2]);
    }

    [Fact]
    public async Task The_wps_file_needs_the_pay_permission()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Hr}/payroll/{Guid.NewGuid()}/wps-sif")).StatusCode);

    [Fact]
    public async Task Performance_reviews_go_from_draft_to_submitted_to_acknowledged_inside_an_open_cycle()
    {
        var x = await SetupAsync();
        var emp = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Layla Haddad", 8_000m)), HttpStatusCode.Created);
        var name = "Annual " + Guid.NewGuid().ToString("N")[..6];
        var cycle = await Ok(await x.C.PostAsJsonAsync($"{Hr}/review-cycles", new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31" }), HttpStatusCode.Created);
        var cycleId = (string)cycle["id"]!;
        Assert.Equal("open", (string)cycle["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/review-cycles", new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/review-cycles", new { name = "Bad", periodStart = "2026-12-31", periodEnd = "2026-01-01" })).StatusCode);

        object Body(int? rating, int score = 4, string goals = "Lease 20 units") => new
        {
            employeeId = (string)emp["id"]!, cycleId, reviewerName = "Ahmed Saleem", overallRating = rating, goals, comments = "Strong year",
            competencies = new[] { new { name = "Communication", score, notes = (string?)"Clear" }, new { name = "Ownership", score = 5, notes = (string?)null } },
        };
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Hr}/reviews", Body(null)), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        Assert.Equal("draft", (string)draft["status"]!);
        Assert.Equal(2, draft["competencies"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/reviews", Body(3))).StatusCode); // one review per employee and cycle
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/reviews", Body(9))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Hr}/reviews/{id}/submit", null)).StatusCode); // no overall rating yet
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Hr}/reviews/{id}/acknowledge", null)).StatusCode); // not submitted

        var edited = await Ok(await x.C.PutAsJsonAsync($"{Hr}/reviews/{id}", Body(4, score: 3, goals: "Lease 25 units")));
        Assert.Equal(4, (int)edited["overallRating"]!);
        Assert.Equal("Lease 25 units", (string)edited["goals"]!);
        Assert.Equal(3, (int)edited["competencies"]!.AsArray().First(c => (string)c!["name"]! == "Communication")!["score"]!);

        var submitted = await Ok(await x.C.PostAsync($"{Hr}/reviews/{id}/submit", null));
        Assert.Equal("submitted", (string)submitted["status"]!);
        Assert.NotNull((string?)submitted["submittedAt"]);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Hr}/reviews/{id}", Body(5))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.DeleteAsync($"{Hr}/reviews/{id}")).StatusCode);

        var done = await Ok(await x.C.PostAsync($"{Hr}/reviews/{id}/acknowledge", null));
        Assert.Equal("acknowledged", (string)done["status"]!);
        Assert.Equal("Layla Haddad", (string)done["employeeName"]!);
        Assert.Contains((await Ok(await x.C.GetAsync($"{Hr}/reviews?pageSize=100"))).AsArray(), r => (string)r!["id"]! == id);

        // a closed cycle takes no new reviews
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/review-cycles/{cycleId}", new { name, periodStart = "2026-01-01", periodEnd = "2026-12-31", status = "closed" }));
        var other = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Yusuf Khan", 4_800m)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/reviews", new { employeeId = (string)other["id"]!, cycleId, competencies = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Draft_reviews_can_be_deleted_and_pending_reviews_show_on_the_dashboard()
    {
        var x = await SetupAsync();
        var emp = await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", Employee(x, "Maria Santos", 11_000m)), HttpStatusCode.Created);
        var cycle = await Ok(await x.C.PostAsJsonAsync($"{Hr}/review-cycles", new { name = "Mid " + Guid.NewGuid().ToString("N")[..6], periodStart = "2026-01-01", periodEnd = "2026-06-30" }), HttpStatusCode.Created);
        var review = await Ok(await x.C.PostAsJsonAsync($"{Hr}/reviews", new { employeeId = (string)emp["id"]!, cycleId = (string)cycle["id"]!, competencies = Array.Empty<object>() }), HttpStatusCode.Created);

        var dash = await Ok(await x.C.GetAsync($"/api/v1/dashboard?legalEntityId={x.Entity}"));
        var kpi = dash["kpis"]!.AsArray().Single(k => (string)k!["key"]! == "reviews-pending")!;
        Assert.Equal(1m, (decimal)kpi["value"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Hr}/reviews/{(string)review["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.C.GetAsync($"{Hr}/reviews/{(string)review["id"]!}")).StatusCode);
    }
}
