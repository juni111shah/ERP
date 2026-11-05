using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>HR summary report and birthdays/anniversaries against known seeded data.</summary>
public class HrReportsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private async Task<(HttpClient C, string Entity, string Amir)> SeedAsync(string? amirBirth = null)
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        // these tests drive the whole flow with one account; the self-approval block has its own tests in HrAccessTests
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.blockSelfApproval"] = false })).StatusCode);
        foreach (var key in new[] { "cf_contract_type", "cf_date_of_birth" })
            await c.PostAsJsonAsync("/api/v1/custom-fields", new { entityType = "hr.employee", key, label = key, dataType = "Text" }); // already there on later calls
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Rep " + Tag(), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

        object Emp(string name, string dept, string nationality, string join, decimal basic, string? contract, string? birth = null, string? terminated = null)
            => new
            {
                legalEntityId = entity, fullName = name, designation = "Agent", department = dept, nationality, joinDate = join, terminationDate = terminated, status = terminated is null ? "active" : "terminated",
                basicSalary = basic, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m,
                customFields = new Dictionary<string, string?> { ["cf_contract_type"] = contract, ["cf_date_of_birth"] = birth },
            };

        var amir = await Ok(await c.PostAsJsonAsync($"{Hr}/employees", Emp("Amir Khan", "Sales", "Indian", "2020-01-01", 6_000m, "Permanent", amirBirth)), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Hr}/employees", Emp("Neha Kapoor", "Sales", "Pakistani", "2023-03-15", 4_000m, "Fixed term")), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Hr}/employees", Emp("Omar Said", "Support", "Indian", "2024-02-01", 3_000m, null, terminated: "2024-06-30")), HttpStatusCode.Created);
        return (c, entity, (string)amir["id"]!);
    }

    [Fact]
    public async Task The_summary_matches_the_seeded_headcount_movements_cost_leave_and_absence()
    {
        var (c, entity, amir) = await SeedAsync();
        var type = (string)(await Ok(await c.PostAsJsonAsync($"{Hr}/leave-types", new { code = "AN" + Tag(), name = "Annual " + Tag(), isPaid = true, annualDays = 20m, carryForwardCap = 0m, requiresApproval = true }), HttpStatusCode.Created))["id"]!;
        var req = await Ok(await c.PostAsJsonAsync($"{Hr}/leave", new { employeeId = amir, leaveTypeId = type, fromDate = "2024-04-08", toDate = "2024-04-12", submit = true }), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Hr}/leave/{(string)req["id"]!}/approve", new { }));
        await Ok(await c.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = amir, year = 2024, month = 11, workingDays = 26, daysPresent = 23, unpaidAbsentDays = 3, paidLeaveDays = 0, overtimeHours = 0 }));
        var run = await Ok(await c.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = entity, year = 2024, month = 10 }), HttpStatusCode.Created);
        await Ok(await c.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));

        var r = await Ok(await c.GetAsync($"{Hr}/reports/summary?from=2024-01-01&to=2024-12-31&legalEntityId={entity}"));
        Assert.Equal(2, (int)r["headcount"]!); // Omar left in June
        Assert.Equal(2, Count(r["byDepartment"]!, "Sales"));
        Assert.Equal(0, Count(r["byDepartment"]!, "Support"));
        Assert.Equal(1, Count(r["byNationality"]!, "Indian"));
        Assert.Equal(1, Count(r["byNationality"]!, "Pakistani"));
        Assert.Equal(1, Count(r["byContractType"]!, "Permanent"));
        Assert.Equal(1, Count(r["byContractType"]!, "Fixed term"));

        var expectedService = Math.Round(((new DateOnly(2024, 12, 31).DayNumber - new DateOnly(2020, 1, 1).DayNumber) + (new DateOnly(2024, 12, 31).DayNumber - new DateOnly(2023, 3, 15).DayNumber)) / 2m / 365.25m, 2);
        Assert.Equal(expectedService, (decimal)r["averageServiceYears"]!);

        var moves = r["movements"]!.AsArray();
        Assert.Equal(12, moves.Count);
        Assert.Equal(1, (int)moves.First(m => (int)m!["month"]! == 2)!["joiners"]!);
        Assert.Equal(1, (int)moves.First(m => (int)m!["month"]! == 6)!["leavers"]!);
        Assert.Equal(1, moves.Sum(m => (int)m!["leavers"]!));

        var cost = r["payrollCost"]!.AsArray().First(m => (int)m!["month"]! == 10)!;
        Assert.Equal(1, (int)cost["runs"]!);
        Assert.Equal(2, (int)cost["employees"]!);
        Assert.Equal(10_000m, (decimal)cost["gross"]!);
        Assert.Equal(10_000m, (decimal)cost["net"]!);
        Assert.Equal(0m, (decimal)r["payrollCost"]!.AsArray().First(m => (int)m!["month"]! == 9)!["net"]!);

        var leave = r["leaveDaysByType"]!.AsArray();
        Assert.Contains(leave, l => ((string)l!["leaveType"]!).StartsWith("Annual") && (decimal)l["days"]! == 5m);
        Assert.Equal(3, (decimal)r["absenceDays"]!);
        Assert.Equal(3m, (decimal)r["absenceByMonth"]!.AsArray().First(m => (int)m!["month"]! == 11)!["days"]!);
    }

    private static int Count(JsonNode list, string key) => list.AsArray().FirstOrDefault(x => (string)x!["key"]! == key) is { } hit ? (int)hit["count"]! : 0;

    [Fact]
    public async Task The_payroll_cost_is_left_out_without_the_payroll_read_permission()
    {
        var (c, entity, _) = await SeedAsync();
        var perms = new[] { "hr.employees.read" };
        var role = await Ok(await c.PostAsJsonAsync("/api/v1/roles", new { name = "HR people " + Tag(), description = "test", permissions = perms }), HttpStatusCode.Created);
        var email = $"hrr{Tag().ToLowerInvariant()}@demo.local";
        (await c.PostAsJsonAsync("/api/v1/users", new { email, fullName = "People only", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)role["id"]! } })).EnsureSuccessStatusCode();
        var (people, _) = await f.SignInAsync(email, "Temp#Passw0rd", workspaceCode: "demo-re");

        var r = await Ok(await people.GetAsync($"{Hr}/reports/summary?from=2024-01-01&to=2024-12-31&legalEntityId={entity}"));
        Assert.Equal(2, (int)r["headcount"]!);
        Assert.Null(r["payrollCost"]);
        var full = await Ok(await c.GetAsync($"{Hr}/reports/summary?from=2024-01-01&to=2024-12-31&legalEntityId={entity}"));
        Assert.NotNull(full["payrollCost"]);
    }

    [Fact]
    public async Task Birthdays_and_anniversaries_list_the_ones_inside_the_window_soonest_first()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var birth = today.AddDays(5).AddYears(-30).ToString("yyyy-MM-dd");
        var (c, entity, _) = await SeedAsync(birth);

        var r = await Ok(await c.GetAsync($"{Hr}/reports/birthdays-anniversaries?days=30&legalEntityId={entity}"));
        var b = r["birthdays"]!.AsArray();
        Assert.Single(b);
        Assert.Equal("Amir Khan", (string)b[0]!["employee"]!);
        Assert.Equal(5, (int)b[0]!["daysAway"]!);
        Assert.Equal(30, (int)b[0]!["years"]!);

        var narrow = await Ok(await c.GetAsync($"{Hr}/reports/birthdays-anniversaries?days=3&legalEntityId={entity}"));
        Assert.Empty(narrow["birthdays"]!.AsArray());

        // Neha joined on 15 March 2023; her anniversary falls inside a window that reaches it.
        var wide = await Ok(await c.GetAsync($"{Hr}/reports/birthdays-anniversaries?days=366&legalEntityId={entity}"));
        var anniversaries = wide["anniversaries"]!.AsArray();
        Assert.Equal(2, anniversaries.Count); // Amir and Neha; Omar has left
        var dates = anniversaries.Select(a => (string)a!["date"]!).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
        Assert.Equal(0, wide["birthdays"]!.AsArray().Count(x => (string)x!["employee"]! == "Omar Said"));
    }

    [Fact]
    public async Task The_reports_need_a_signed_in_user_and_a_sensible_period()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Hr}/reports/summary")).StatusCode);
        var (c, _, _) = await SeedAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Hr}/reports/summary?from=2024-12-31&to=2024-01-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Hr}/reports/birthdays-anniversaries?days=0")).StatusCode);
    }
}
