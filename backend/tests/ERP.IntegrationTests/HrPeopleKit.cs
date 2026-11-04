using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;

namespace ERP.IntegrationTests;

/// <summary>Shared setup for the HR access and self-service tests: an owner client, a fresh employing entity and helpers that create limited-permission users, employees and leave types.</summary>
internal sealed class HrKit
{
    public const string Hr = "/api/v1/m/hr";
    public const string Password = "Temp#Passw0rd";

    private readonly ApiFactory _f;
    public HttpClient Owner { get; private set; } = default!;
    public string Entity { get; private set; } = default!;

    private HrKit(ApiFactory f) => _f = f;

    public static async Task<HrKit> StartAsync(ApiFactory f)
    {
        var kit = new HrKit(f);
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (owner, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        kit.Owner = owner;
        kit.Entity = (string)(await Ok(await owner.PostAsJsonAsync("/api/v1/legal-entities", new { name = "People " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        return kit;
    }

    public static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    public static async Task<JsonObject> ErrorOf(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return JsonNode.Parse(text)!.AsObject();
    }

    public static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..6];

    /// <summary>A user with exactly the given permissions (a role of its own), signed in.</summary>
    public async Task<(HttpClient Client, string UserId)> UserAsync(string label, params string[] permissions)
    {
        var role = await Ok(await Owner.PostAsJsonAsync("/api/v1/roles", new RoleInput(Unique("Role" + label), "test", permissions)), HttpStatusCode.Created);
        var email = $"{label.ToLowerInvariant()}_{Guid.NewGuid():N}@demo.local";
        var user = await Ok(await Owner.PostAsJsonAsync("/api/v1/users", new { email, fullName = "User " + label, temporaryPassword = Password, roleIds = new[] { (string)role["id"]! } }), HttpStatusCode.Created);
        var (client, _) = await _f.SignInAsync(email, Password, "demo-re");
        return (client, (string)user["id"]!);
    }

    public JsonObject EmployeeBody(string name, decimal basic = 3_000m, Action<JsonObject>? tweak = null)
    {
        var body = new JsonObject
        {
            ["legalEntityId"] = Entity, ["fullName"] = name, ["designation"] = "Agent", ["joinDate"] = "2020-01-01", ["status"] = "active",
            ["basicSalary"] = basic, ["housingAllowance"] = 500m, ["transportAllowance"] = 200m, ["otherAllowances"] = 0m, ["recurringDeductions"] = 100m,
            ["iban"] = "AE070331234567890123456",
        };
        tweak?.Invoke(body);
        return body;
    }

    public async Task<string> EmployeeAsync(string name, decimal basic = 3_000m, Action<JsonObject>? tweak = null)
        => (string)(await Ok(await Owner.PostAsJsonAsync($"{Hr}/employees", EmployeeBody(name, basic, tweak)), HttpStatusCode.Created))["id"]!;

    /// <summary>A paid leave type with no balance to enforce (annual days 0), so tests can book any dates.</summary>
    public async Task<string> LeaveTypeAsync()
        => (string)(await Ok(await Owner.PostAsJsonAsync($"{Hr}/leave-types", new { code = Unique("LT"), name = "Test leave", isPaid = true, annualDays = 0m, carryForwardCap = 0m, requiresApproval = true }), HttpStatusCode.Created))["id"]!;

    public async Task SetAsync(string key, object value)
        => Assert.Equal(HttpStatusCode.NoContent, (await Owner.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { [key] = value })).StatusCode);
}

/// <summary>Permission keys as the API knows them (the tests do not reference the module assembly).</summary>
internal static class Perm
{
    public const string EmployeesRead = "hr.employees.read", EmployeesManage = "hr.employees.manage", SalaryRead = "hr.salary.read";
    public const string LeaveRead = "hr.leave.read", LeaveRequest = "hr.leave.request", LeaveApprove = "hr.leave.approve", LeaveManage = "hr.leave.manage";
    public const string PayrollRead = "hr.payroll.read", PayrollPost = "hr.payroll.post", PayrollPay = "hr.payroll.pay";
}
