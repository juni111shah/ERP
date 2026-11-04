using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Employee documents: CRUD, attachments, the expiring list and the dashboard alert.</summary>
public class HrDocumentsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string Iso(int daysFromToday) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(daysFromToday).ToString("yyyy-MM-dd");

    private async Task<(HttpClient C, string Entity, string EmployeeId)> SetupAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Docs " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var emp = await Ok(await c.PostAsJsonAsync($"{Hr}/employees", new { legalEntityId = entity, fullName = "Amir Khan", designation = "Agent", joinDate = "2020-01-01", status = "active", basicSalary = 6000m, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m }), HttpStatusCode.Created);
        return (c, entity, (string)emp["id"]!);
    }

    private static Task<HttpResponseMessage> CreateDoc(HttpClient c, string employeeId, string type, int? expiresInDays, string? number = null)
        => c.PostAsJsonAsync($"{Hr}/documents", new { employeeId, type, number = number ?? type + "-123", issueDate = "2020-01-01", expiryDate = expiresInDays is { } d ? Iso(d) : null, issuedBy = "Authority" });

    [Fact]
    public async Task A_document_can_be_created_updated_attached_and_deleted()
    {
        var (c, _, emp) = await SetupAsync();
        var doc = await Ok(await CreateDoc(c, emp, "passport", 400), HttpStatusCode.Created);
        var id = (string)doc["id"]!;
        Assert.Equal(400, (int)doc["daysLeft"]!);
        Assert.False((bool)doc["isExpired"]!);

        var form = new MultipartFormDataContent
        {
            { new StringContent("hr.employeedocument"), "ownerType" }, { new StringContent(id), "ownerId" }, { new StringContent("Passport scan"), "category" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("scan")), "file", "passport.txt" },
        };
        var up = await Ok(await c.PostAsync("/api/v1/attachments", form), HttpStatusCode.Created);
        Assert.Equal("passport.txt", (string)up["fileName"]!);
        var listed = await Ok(await c.GetAsync($"/api/v1/attachments?ownerType=hr.employeedocument&ownerId={id}"));
        Assert.Single(listed.AsArray());

        var put = await Ok(await c.PutAsJsonAsync($"{Hr}/documents/{id}", new { employeeId = emp, type = "passport", number = "P-999", issueDate = "2020-01-01", expiryDate = Iso(-1) }));
        Assert.Equal("P-999", (string)put["number"]!);
        Assert.True((bool)put["isExpired"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Hr}/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Hr}/documents/{id}")).StatusCode);
    }

    [Fact]
    public async Task The_expiring_list_applies_the_window_flags_expired_and_is_ordered_by_expiry()
    {
        var (c, _, emp) = await SetupAsync();
        var expired = (string)(await Ok(await CreateDoc(c, emp, "visa", -5), HttpStatusCode.Created))["id"]!;
        var soon = (string)(await Ok(await CreateDoc(c, emp, "residenceId", 20), HttpStatusCode.Created))["id"]!;
        var later = (string)(await Ok(await CreateDoc(c, emp, "labourCard", 55), HttpStatusCode.Created))["id"]!;
        await Ok(await CreateDoc(c, emp, "medical", 200), HttpStatusCode.Created);
        await Ok(await CreateDoc(c, emp, "contract", null), HttpStatusCode.Created); // no expiry

        var rows = (await Ok(await c.GetAsync($"{Hr}/documents/expiring?days=60"))).AsArray();
        var ids = rows.Select(r => (string)r!["id"]!).ToList();
        Assert.Equal([expired, soon, later], ids.Where(i => i == expired || i == soon || i == later).ToList());
        Assert.DoesNotContain(rows, r => (string)r!["type"]! is "medical" or "contract");
        var first = rows.First(r => (string)r!["id"]! == expired)!;
        Assert.True((bool)first["isExpired"]!);
        Assert.Equal(-5, (int)first["daysLeft"]!);
        Assert.Equal("Amir Khan", (string)first["employeeName"]!);

        var narrow = (await Ok(await c.GetAsync($"{Hr}/documents/expiring?days=30"))).AsArray().Select(r => (string)r!["id"]!).ToList();
        Assert.Contains(soon, narrow);
        Assert.DoesNotContain(later, narrow);
    }

    [Fact]
    public async Task The_dashboard_shows_an_alert_for_expiring_and_a_critical_one_for_expired_documents()
    {
        var (c, entity, emp) = await SetupAsync();
        await Ok(await CreateDoc(c, emp, "visa", 10), HttpStatusCode.Created);
        var d = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}"));
        var alerts = d["alerts"]!.AsArray();
        Assert.Contains(alerts, a => (string)a!["message"]! == "1 document(s) expire within 30 days." && (string)a["severity"]! == "warning");
        Assert.DoesNotContain(alerts, a => ((string)a!["message"]!).Contains("have expired"));

        await Ok(await CreateDoc(c, emp, "passport", -2), HttpStatusCode.Created);
        d = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}"));
        Assert.Contains(d["alerts"]!.AsArray(), a => (string)a!["message"]! == "1 employee document(s) have expired." && (string)a["severity"]! == "critical");
    }

    [Fact]
    public async Task The_reminder_source_fires_once_per_stage_for_a_document_close_to_expiry()
    {
        var (c, _, emp) = await SetupAsync();
        await Ok(await CreateDoc(c, emp, "visa", 25), HttpStatusCode.Created);
        var first = (int)(await Ok(await c.PostAsync("/api/v1/messages/send-reminders", null)))["sent"]!;
        Assert.True(first >= 1);
        var again = await Ok(await c.PostAsync("/api/v1/messages/send-reminders", null));
        Assert.Equal(0, (int)again["sent"]!);
    }

    [Fact]
    public async Task Documents_need_a_signed_in_user_with_the_employee_permissions()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Hr}/documents/expiring")).StatusCode);

    [Fact]
    public async Task An_expiry_before_the_issue_date_is_refused()
    {
        var (c, _, emp) = await SetupAsync();
        var res = await c.PostAsJsonAsync($"{Hr}/documents", new { employeeId = emp, type = "visa", issueDate = "2026-05-01", expiryDate = "2026-04-01" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
