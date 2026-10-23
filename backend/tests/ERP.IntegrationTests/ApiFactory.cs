using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Auth;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

public sealed class CapturingResetSender : IPasswordResetSender
{
    public ConcurrentDictionary<string, string> Tokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Task SendAsync(string email, string token, CancellationToken ct = default) { Tokens[email] = token; return Task.CompletedTask; }
}

/// <summary>API host on an in-memory SQLite database (a test-only harness; the app itself runs on SQL Server only), seeded through the normal onboarding path.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test#Passw0rd";
    public const string AdminEmail = "admin@demo.local";
    public const string OwnerEmail = "owner@demo.local";
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public CapturingResetSender Resets { get; } = new();
    public CapturingEmailChannel Emails { get; } = new();

    public ApiFactory() => _conn.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=unused-in-tests", // the DbContext is replaced below
            ["Database:AutoMigrate"] = "true",
            ["Auth:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["Auth:SecureCookies"] = "true",
            ["Onboarding:AllowSelfSignup"] = "true",
            ["Seed:Enabled"] = "true",
            ["Jobs:Enabled"] = "false",
            ["Seed:AdminEmail"] = AdminEmail,
            ["Seed:MultiWorkspaceEmail"] = OwnerEmail,
            ["Seed:AdminPassword"] = Password,
            ["RateLimiting:LoginPerMinute"] = "10000",
            ["Storage:Root"] = Path.Combine(Path.GetTempPath(), "erp-tests-" + Guid.NewGuid().ToString("N")),
        };
        // UseSetting is visible to Program's top-level code before Build() (minimal hosting).
        foreach (var (k, v) in settings) builder.UseSetting(k, v);
        builder.ConfigureServices(s =>
        {
            // Swap the app's SQL Server DbContext for the in-memory SQLite connection, and build the schema from the
            // model (SQLite cannot run the SQL Server migrations).
            s.RemoveAll<DbContextOptions<AppDbContext>>();
            s.AddDbContext<AppDbContext>((sp, o) =>
            {
                o.UseSqlite(_conn);
                o.ReplaceService<IModelCacheKeyFactory, ModuleAwareModelCacheKeyFactory>();
                o.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });
            s.RemoveAll<IDatabaseInitializer>();
            s.AddScoped<IDatabaseInitializer, EnsureCreatedInitializer>();
            s.RemoveAll<IPasswordResetSender>();
            s.AddSingleton<IPasswordResetSender>(Resets);
            s.RemoveAll<ERP.Application.Reminders.IMessageChannel>();
            s.AddScoped<ERP.Application.Reminders.IMessageChannel, ERP.Application.Reminders.InAppChannel>();
            s.AddSingleton(Emails);
            s.AddScoped<ERP.Application.Reminders.IMessageChannel>(sp => sp.GetRequiredService<CapturingEmailChannel>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _conn.Dispose();
    }

    public HttpClient Client()
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        c.DefaultRequestHeaders.Add("X-ERP-Client", "tests");
        return c;
    }

    /// <summary>Signs in (single-workspace users) and returns an authorised client.</summary>
    public async Task<(HttpClient Client, JsonObject Session)> SignInAsync(string email, string password = Password, string? workspaceCode = null)
    {
        var c = Client();
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = (await Json(res)).AsObject();
        if (body["requiresWorkspaceSelection"]?.GetValue<bool>() == true)
        {
            var ws = body["workspaces"]!.AsArray().First(w => workspaceCode is null || (string?)w!["code"] == workspaceCode)!;
            res = await c.PostAsJsonAsync("/api/v1/auth/select-workspace", new { selectionToken = (string)body["selectionToken"]!, workspaceId = (string)ws["id"]! });
            res.EnsureSuccessStatusCode();
            body = (await Json(res)).AsObject();
        }
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)body["accessToken"]!);
        return (c, body);
    }

    /// <summary>Parses a response and unwraps the ApiResponse envelope: success → data (paged → the items array); errors → the whole envelope.</summary>
    public static async Task<JsonNode> Json(HttpResponseMessage res) => Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    public static async Task<JsonObject> Envelope(HttpResponseMessage res) => JsonNode.Parse(await res.Content.ReadAsStringAsync())!.AsObject();

    public static JsonNode Unwrap(JsonNode body) =>
        body is JsonObject o && o.ContainsKey("success") && o["success"]!.GetValue<bool>() ? o["data"]?.DeepClone() ?? new JsonObject() : body;
}

/// <summary>Test-only schema setup: creates the tables straight from the model.</summary>
public sealed class EnsureCreatedInitializer : IDatabaseInitializer
{
    public Task InitializeAsync(AppDbContext db, CancellationToken ct = default) => db.Database.EnsureCreatedAsync(ct);
}

/// <summary>Captures outbound email so tests can read one-time codes.</summary>
public sealed class CapturingEmailChannel : ERP.Application.Reminders.IMessageChannel
{
    public string Name => "email";
    public System.Collections.Concurrent.ConcurrentQueue<ERP.Application.Reminders.OutboundMessage> Sent { get; } = new();
    public Task SendAsync(ERP.Application.Reminders.OutboundMessage message, CancellationToken ct) { Sent.Enqueue(message); return Task.CompletedTask; }
    public string? LastCodeFor(string email) => Sent.Where(m => m.Recipient == email).Select(m => System.Text.RegularExpressions.Regex.Match(m.Body ?? "", @"\b\d{6}\b").Value).LastOrDefault(v => v != "");
}
