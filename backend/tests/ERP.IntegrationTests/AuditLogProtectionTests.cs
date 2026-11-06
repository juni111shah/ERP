using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>What the audit log may hold and who may read its values: secrets never enter it; sensitive record types need their own read permission.</summary>
public class AuditLogProtectionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Salary = "98765.43";
    private const string Name = "Zedekiah Quillfeather";

    private async Task<List<AuditEvent>> EventsAsync(Func<IQueryable<AuditEvent>, IQueryable<AuditEvent>> query)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db.AuditEvents.IgnoreQueryFilters()).AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Enrolling_an_authenticator_writes_no_identity_token_or_secret_to_the_audit_log()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var role = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new RoleInput(HrKit.Unique("Plain"), null, [CorePermissions.PartiesRead])), HttpStatusCode.Created))["id"]!;
        var email = HrKit.Unique("enrol") + "@demo.local";
        var userId = Guid.Parse((string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Enrolling User", temporaryPassword = HrKit.Password, roleIds = new[] { role } }), HttpStatusCode.Created))["id"]!);
        var (c, _) = await f.SignInAsync(email, HrKit.Password);

        var setup = await HrKit.Ok(await c.PostAsync("/api/v1/auth/2fa/setup", null));
        var secret = (string)setup["secret"]!;

        Assert.Empty(await EventsAsync(q => q.Where(a => a.UserId == userId && a.RecordType.StartsWith("IdentityUser"))));
        Assert.Empty(await EventsAsync(q => q.Where(a => (a.After != null && a.After.Contains(secret)) || (a.Before != null && a.Before.Contains(secret)))));
    }

    [Fact]
    public async Task Idempotency_records_stay_out_of_the_audit_log()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/parties") { Content = JsonContent.Create(new { name = HrKit.Unique("Idem Audit"), kind = "Company", roles = "Customer" }) };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await HrKit.Ok(await admin.SendAsync(req), HttpStatusCode.Created);

        Assert.Empty(await EventsAsync(q => q.Where(a => a.RecordType == nameof(IdempotencyRecord))));
    }

    [Fact]
    public async Task A_secret_already_in_the_log_from_before_the_fix_cannot_be_read_back()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var party = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/parties", new { name = HrKit.Unique("Legacy"), kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var orgId = (await EventsAsync(q => q.Where(a => a.RecordId == party))).First().OrgId;
        var legacy = new AuditEvent
        {
            OrgId = orgId, OccurredAt = DateTime.UtcNow, Action = "Created", RecordType = "IdentityUserToken`1", RecordId = Guid.NewGuid().ToString(),
            After = """{"Name":"AuthenticatorKey","Value":"LEGACYSECRETVALUE"}""",
        };
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AuditEvents.Add(legacy);
            await db.SaveChangesAsync();
        }

        var detail = await admin.GetAsync($"/api/v1/audit/{legacy.Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain("LEGACYSECRETVALUE", await detail.Content.ReadAsStringAsync());
        var history = await admin.GetAsync($"/api/v1/audit/record?recordType={Uri.EscapeDataString(legacy.RecordType)}&recordId={legacy.RecordId}");
        Assert.DoesNotContain("LEGACYSECRETVALUE", await history.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Without_the_salary_permission_the_audit_log_shows_who_when_and_which_fields_but_no_values()
    {
        var k = await HrKit.StartAsync(f);
        var id = await k.EmployeeAsync(Name, 98_765.43m);
        var changed = k.EmployeeBody(Name, 12_345.67m);
        await HrKit.Ok(await k.Owner.PutAsJsonAsync($"{HrKit.Hr}/employees/{id}", changed));
        var (auditor, _) = await k.UserAsync("Auditor", CorePermissions.AuditRead);

        // the list: who, when and which fields, without the record's name
        var listBody = await (await auditor.GetAsync($"/api/v1/audit?filter=recordType:eq:Employee&filter=recordId:eq:{id}&sort=-occurredAt")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Name, listBody);
        var rows = ApiFactory.Unwrap(JsonNode.Parse(listBody)!).AsArray();
        var updated = rows.First(r => (string)r!["action"]! == "Updated")!;
        Assert.True((bool)updated["valuesHidden"]!);
        Assert.Null((string?)updated["recordLabel"]);
        Assert.Contains("BasicSalary", updated["changedFields"]!.AsArray().Select(x => (string)x!));
        Assert.NotNull(updated["occurredAt"]);
        Assert.False(string.IsNullOrEmpty((string?)updated["userName"]));

        // the detail: field names only
        var detailBody = await (await auditor.GetAsync($"/api/v1/audit/{updated["id"]}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Salary, detailBody);
        Assert.DoesNotContain("12345.67", detailBody);
        Assert.DoesNotContain(Name, detailBody);
        var detail = ApiFactory.Unwrap(JsonNode.Parse(detailBody)!);
        Assert.Null(detail["before"]);
        Assert.Null(detail["after"]);
        var salaryField = detail["fields"]!.AsArray().Single(x => (string)x!["field"]! == "BasicSalary")!;
        Assert.Null((string?)salaryField["old"]);
        Assert.Null((string?)salaryField["new"]);

        // the per-record history: the same
        var historyBody = await (await auditor.GetAsync($"/api/v1/audit/record?recordType=Employee&recordId={id}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Salary, historyBody);
        Assert.DoesNotContain("12345.67", historyBody);
        Assert.DoesNotContain(Name, historyBody);
        var change = ApiFactory.Unwrap(JsonNode.Parse(historyBody)!).AsArray().First(e => (string)e!["action"]! == "Updated")!;
        Assert.True((bool)change["valuesHidden"]!);
        var salaryChange = change["changes"]!.AsArray().Single(x => (string)x!["field"]! == "BasicSalary")!;
        Assert.Null((string?)salaryChange["old"]);
        Assert.Null((string?)salaryChange["new"]);
    }

    [Fact]
    public async Task With_the_salary_permission_the_values_are_shown_and_other_record_types_are_unaffected()
    {
        var k = await HrKit.StartAsync(f);
        var id = await k.EmployeeAsync(Name, 98_765.43m);
        await HrKit.Ok(await k.Owner.PutAsJsonAsync($"{HrKit.Hr}/employees/{id}", k.EmployeeBody(Name, 12_345.67m)));

        var (payAuditor, _) = await k.UserAsync("PayAuditor", CorePermissions.AuditRead, Perm.SalaryRead);
        var history = (await HrKit.Ok(await payAuditor.GetAsync($"/api/v1/audit/record?recordType=Employee&recordId={id}"))).AsArray();
        var change = history.First(e => (string)e!["action"]! == "Updated")!;
        Assert.False((bool)change["valuesHidden"]!);
        Assert.Contains("12345.67", change["changes"]!.AsArray().Single(x => (string)x!["field"]! == "BasicSalary")!["new"]!.ToString());

        var party = (string)(await HrKit.Ok(await k.Owner.PostAsJsonAsync("/api/v1/parties", new { name = "Open Record Co", kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var (auditor, _) = await k.UserAsync("PlainAuditor", CorePermissions.AuditRead);
        var partyHistory = (await HrKit.Ok(await auditor.GetAsync($"/api/v1/audit/record?recordType=Party&recordId={party}"))).AsArray();
        Assert.Equal("Open Record Co", (string)partyHistory[0]!["changes"]!.AsArray().Single(x => (string)x!["field"]! == "Name")!["new"]!);
    }
}
