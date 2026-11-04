using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Payslips: JSON, PDF and email for posted runs; refused for drafts; bulk email counts sent and skipped.</summary>
public class PayslipTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private sealed class FakeSender(bool enabled) : IEmailSender
    {
        public bool IsEnabled => enabled;
        public List<(string To, string Subject, string Text)> Sent { get; } = [];
        public Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
        {
            lock (Sent) Sent.Add((to, subject, bodyText));
            return Task.CompletedTask;
        }
    }

    private WebApplicationFactory<Program> With(FakeSender fake) => f.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IEmailSender>(); s.AddSingleton<IEmailSender>(fake); }));

    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> app)
    {
        await (await SignInAsync(app)).PostAsync("/api/v1/modules/hr/enable", null);
        return await SignInAsync(app); // sign in again so the token carries the HR permissions
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password });
        var body = ApiFactory.Unwrap(JsonNode.Parse(await login.Content.ReadAsStringAsync())!);
        var token = (string?)body["accessToken"];
        if (token is null)
        {
            var sel = await client.PostAsJsonAsync("/api/v1/auth/select-workspace", new { selectionToken = (string)body["selectionToken"]!, workspaceId = (string)body["workspaces"]![0]!["id"]! });
            token = (string)ApiFactory.Unwrap(JsonNode.Parse(await sel.Content.ReadAsStringAsync())!)["accessToken"]!;
        }
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static object Emp(string entity, string name, decimal basic, string? email) => new
    {
        legalEntityId = entity, fullName = name, designation = "Agent", department = "Leasing", joinDate = "2020-01-01", status = "active", basicSalary = basic,
        housingAllowance = 2_000m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 500m, email,
    };

    /// <summary>A draft run for two employees (Amir with an email, Neha without), Amir with an unpaid day and overtime.</summary>
    private static async Task<(string Entity, string RunId, JsonArray Lines)> DraftRunAsync(HttpClient c)
    {
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Slip " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var amir = await Ok(await c.PostAsJsonAsync($"{Hr}/employees", Emp(entity, "Amir Khan", 6_000m, "amir@example.com")), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Hr}/employees", Emp(entity, "Neha Kapoor", 12_000m, null)), HttpStatusCode.Created);
        await Ok(await c.PutAsJsonAsync($"{Hr}/attendance", new { employeeId = (string)amir["id"]!, year = 2026, month = 10, workingDays = 26, daysPresent = 25, unpaidAbsentDays = 1, paidLeaveDays = 0, overtimeHours = 10 }));
        var run = await Ok(await c.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        return (entity, (string)run["id"]!, run["lines"]!.AsArray());
    }

    [Fact]
    public async Task A_draft_run_has_no_payslips()
    {
        var app = With(new FakeSender(true));
        var c = await AdminAsync(app);
        var (_, id, lines) = await DraftRunAsync(c);
        var line = (string)lines[0]!["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"{Hr}/payroll/{id}/payslips/{line}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"{Hr}/payroll/{id}/payslips/{line}/payslip.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Hr}/payroll/{id}/payslips/{line}/email", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Hr}/payroll/{id}/email-payslips", null)).StatusCode);
    }

    [Fact]
    public async Task A_posted_run_gives_the_payslip_json_and_a_valid_pdf()
    {
        var app = With(new FakeSender(true));
        var c = await AdminAsync(app);
        var (_, id, lines) = await DraftRunAsync(c);
        await Ok(await c.PostAsync($"{Hr}/payroll/{id}/post", null));
        var amir = lines.First(l => (string)l!["employeeName"]! == "Amir Khan")!;
        var lineId = (string)amir["id"]!;

        var slip = await Ok(await c.GetAsync($"{Hr}/payroll/{id}/payslips/{lineId}"));
        Assert.Equal("Amir Khan", (string)slip["employeeName"]!);
        Assert.Equal("AED", (string)slip["currency"]!);
        Assert.Equal(10, (int)slip["month"]!);
        // Basic 6,000 + housing 2,000 = 8,000; 1 unpaid day = 266.67; overtime 6,000/30/8 x 1.25 x 10 = 312.50; recovery 500.
        var net = (decimal)amir["netPay"]!;
        Assert.Equal(net, (decimal)slip["netPay"]!);
        Assert.Equal((decimal)slip["totalEarnings"]! - (decimal)slip["totalDeductions"]!, net);
        Assert.Equal(net, (decimal)slip["ytdNet"]!);
        Assert.Contains(slip["earnings"]!.AsArray(), e => (string)e!["label"]! == "Basic salary" && (decimal)e["amount"]! == 6_000m);
        Assert.Contains(slip["deductions"]!.AsArray(), e => ((string)e!["label"]!).StartsWith("Unpaid absence"));

        var pdf = await c.GetAsync($"{Hr}/payroll/{id}/payslips/{lineId}/payslip.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.True(bytes.Length > 1_000);
        Assert.Contains("Payslip", pdf.Content.Headers.ContentDisposition?.FileName ?? "");
    }

    [Fact]
    public async Task A_payslip_is_emailed_to_the_employee_or_to_a_typed_address()
    {
        var fake = new FakeSender(true);
        var c = await AdminAsync(With(fake));
        var (_, id, lines) = await DraftRunAsync(c);
        await Ok(await c.PostAsync($"{Hr}/payroll/{id}/post", null));
        var amir = (string)lines.First(l => (string)l!["employeeName"]! == "Amir Khan")!["id"]!;
        var neha = (string)lines.First(l => (string)l!["employeeName"]! == "Neha Kapoor")!["id"]!;

        var sent = await Ok(await c.PostAsJsonAsync($"{Hr}/payroll/{id}/payslips/{amir}/email", new { }));
        Assert.Equal("amir@example.com", (string)sent["to"]!);
        Assert.Contains("Payslip", (string)sent["subject"]!);
        Assert.Contains(fake.Sent, m => m.To == "amir@example.com" && m.Text.Contains("Net pay"));

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Hr}/payroll/{id}/payslips/{neha}/email", new { })).StatusCode); // no address on file
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"{Hr}/payroll/{id}/payslips/{neha}/email", new { to = "neha.private@example.com" })).StatusCode);
    }

    [Fact]
    public async Task The_bulk_email_reports_how_many_were_sent_and_skipped()
    {
        var fake = new FakeSender(true);
        var c = await AdminAsync(With(fake));
        var (_, id, _) = await DraftRunAsync(c);
        await Ok(await c.PostAsync($"{Hr}/payroll/{id}/post", null));
        var result = await Ok(await c.PostAsync($"{Hr}/payroll/{id}/email-payslips", null));
        Assert.Equal(1, (int)result["sent"]!);
        Assert.Equal(1, (int)result["skipped"]!);
        Assert.Contains("Neha Kapoor", (string)result["skippedEmployees"]![0]!);
        Assert.Single(fake.Sent, m => m.To == "amir@example.com");
    }

    [Fact]
    public async Task Emailing_needs_email_to_be_configured()
    {
        var c = await AdminAsync(With(new FakeSender(false)));
        var (_, id, _) = await DraftRunAsync(c);
        await Ok(await c.PostAsync($"{Hr}/payroll/{id}/post", null));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Hr}/payroll/{id}/email-payslips", null)).StatusCode);
    }

    [Fact]
    public async Task Payslips_need_a_signed_in_user()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Hr}/payroll/{Guid.NewGuid()}/payslips/{Guid.NewGuid()}")).StatusCode);
}
