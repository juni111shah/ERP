using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Reminders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Email delivery: the reminder email channel records successes and failures, retries failed sends up to three times, and the test-email action.</summary>
public class EmailTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private sealed class FakeEmailSender(bool enabled, int failFirst) : IEmailSender
    {
        private int _calls;
        public bool IsEnabled => enabled;
        public List<(string To, string Subject)> Sent { get; } = [];
        public int Calls => _calls;
        public Task SendAsync(string to, string subject, string bodyText, string? bodyHtml = null, CancellationToken ct = default)
        {
            var n = Interlocked.Increment(ref _calls);
            if (n <= failFirst) throw new InvalidOperationException("550 mailbox unavailable");
            lock (Sent) Sent.Add((to, subject));
            return Task.CompletedTask;
        }
    }

    private sealed class OneEmailReminder(string key, string to) : IReminderSource
    {
        public string? ModuleKey => null;
        public Task<IReadOnlyList<ReminderCandidate>> GetDueAsync(DateOnly today, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ReminderCandidate>>([new ReminderCandidate(key, "Email reminder " + key, "Body text", null, null, to)]);
    }

    private WebApplicationFactory<Program> With(FakeEmailSender fake, string key, string to) => f.WithWebHostBuilder(b => b.ConfigureServices(s =>
    {
        s.RemoveAll<IEmailSender>();
        s.AddSingleton<IEmailSender>(fake);
        s.RemoveAll<IMessageChannel>(); // ApiFactory swaps in a capturing email channel for OTP tests; here the real one under test is used
        s.AddScoped<IMessageChannel, InAppChannel>();
        s.AddScoped<IMessageChannel, OutboxEmailChannel>();
        s.AddScoped<IReminderSource>(_ => new OneEmailReminder(key, to));
    }));

    private static async Task<HttpClient> AdminAsync(WebApplicationFactory<Program> app)
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

    private static async Task<JsonNode> Json(HttpResponseMessage r) => ApiFactory.Unwrap(JsonNode.Parse(await r.Content.ReadAsStringAsync())!);

    private static async Task<JsonNode> EmailRow(HttpClient c, string key)
        => (await Json(await c.GetAsync($"/api/v1/messages?search={Uri.EscapeDataString(key)}&pageSize=20"))).AsArray().Single(m => (string)m!["channel"]! == "email")!;

    [Fact]
    public async Task A_failed_send_is_logged_then_retried_and_a_delivered_one_is_never_repeated()
    {
        var key = "em-" + Guid.NewGuid().ToString("N")[..8];
        var to = key + "@example.com";
        var fake = new FakeEmailSender(enabled: true, failFirst: 1);
        await using var app = With(fake, key, to);
        var c = await AdminAsync(app);

        await c.PostAsync("/api/v1/messages/send-reminders", null);
        var row = await EmailRow(c, key);
        Assert.Equal("failed", (string)row["status"]!);
        Assert.Contains("550", (string)row["error"]!);
        Assert.Equal(1, (int)row["attempts"]!);
        Assert.Empty(fake.Sent);

        await c.PostAsync("/api/v1/messages/send-reminders", null); // the next run retries and succeeds
        row = await EmailRow(c, key);
        Assert.Equal("sent", (string)row["status"]!);
        Assert.Equal(2, (int)row["attempts"]!);
        Assert.Equal(to, Assert.Single(fake.Sent).To);

        await c.PostAsync("/api/v1/messages/send-reminders", null);
        Assert.Single(fake.Sent); // delivered once only
    }

    [Fact]
    public async Task A_send_that_keeps_failing_is_given_up_after_three_attempts()
    {
        var key = "em-" + Guid.NewGuid().ToString("N")[..8];
        var fake = new FakeEmailSender(enabled: true, failFirst: int.MaxValue);
        await using var app = With(fake, key, key + "@example.com");
        var c = await AdminAsync(app);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/v1/messages/send-reminders", null)).StatusCode);
        var row = await EmailRow(c, key);
        Assert.Equal(3, (int)row["attempts"]!);
        Assert.Equal(3, fake.Calls);
    }

    [Fact]
    public async Task Test_email_sends_logs_and_reports_misconfiguration()
    {
        var fake = new FakeEmailSender(enabled: true, failFirst: 0);
        await using (var app = With(fake, "unused", "unused@example.com"))
        {
            var c = await AdminAsync(app);
            var res = await c.PostAsJsonAsync("/api/v1/messages/test-email", new { to = "admin-check@example.com" });
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("sent", (string)(await Json(res))["status"]!);
            Assert.Equal("admin-check@example.com", Assert.Single(fake.Sent).To);
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/messages/test-email", new { to = "not-an-email" })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.CreateClient().PostAsJsonAsync("/api/v1/messages/test-email", new { to = "a@example.com" })).StatusCode);
        }
        await using (var off = With(new FakeEmailSender(enabled: false, failFirst: 0), "unused2", "unused2@example.com"))
        {
            var c = await AdminAsync(off);
            Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/v1/messages/test-email", new { to = "a@example.com" })).StatusCode);
        }
    }

    [Fact]
    public async Task Password_reset_goes_by_email_when_it_is_enabled()
    {
        var fake = new FakeEmailSender(enabled: true, failFirst: 0);
        await using var app = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IEmailSender>();
            s.AddSingleton<IEmailSender>(fake);
            s.RemoveAll<ERP.Infrastructure.Auth.IPasswordResetSender>(); // ApiFactory captures resets; use the real sender here
            s.AddSingleton<ERP.Infrastructure.Auth.IPasswordResetSender, ERP.Infrastructure.Auth.LoggingPasswordResetSender>();
        }));
        var res = await app.CreateClient().PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = ApiFactory.AdminEmail });
        Assert.True(res.IsSuccessStatusCode);
        var mail = Assert.Single(fake.Sent);
        Assert.Equal(ApiFactory.AdminEmail, mail.To);
        Assert.Contains("password", mail.Subject, StringComparison.OrdinalIgnoreCase);
    }
}
