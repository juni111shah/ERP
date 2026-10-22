using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

public sealed class CapturingResetSender : IPasswordResetSender
{
    public ConcurrentDictionary<string, string> Tokens { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Task SendAsync(string email, string token, CancellationToken ct = default) { Tokens[email] = token; return Task.CompletedTask; }
}

/// <summary>API host on an in-memory SQLite database, seeded through the normal onboarding path.</summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test#Passw0rd";
    public const string AdminEmail = "admin@demo.local";
    public const string OwnerEmail = "owner@demo.local";
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public CapturingResetSender Resets { get; } = new();

    public ApiFactory() => _conn.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "DataSource=:memory:",
            ["Database:Provider"] = "Sqlite",
            ["Database:AutoMigrate"] = "true",
            ["Auth:SigningKey"] = "integration-tests-signing-key-0123456789abcdef",
            ["Auth:SecureCookies"] = "true",
            ["Onboarding:AllowSelfSignup"] = "true",
            ["Seed:Enabled"] = "true",
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
            s.AddSingleton(_conn);
            s.RemoveAll<IPasswordResetSender>();
            s.AddSingleton<IPasswordResetSender>(Resets);
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
        var body = (await res.Content.ReadFromJsonAsync<JsonObject>())!;
        if (body["requiresWorkspaceSelection"]?.GetValue<bool>() == true)
        {
            var ws = body["workspaces"]!.AsArray().First(w => workspaceCode is null || (string?)w!["code"] == workspaceCode)!;
            res = await c.PostAsJsonAsync("/api/v1/auth/select-workspace", new { selectionToken = (string)body["selectionToken"]!, workspaceId = (string)ws["id"]! });
            res.EnsureSuccessStatusCode();
            body = (await res.Content.ReadFromJsonAsync<JsonObject>())!;
        }
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)body["accessToken"]!);
        return (c, body);
    }

    public static async Task<JsonNode> Json(HttpResponseMessage res) => JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
}
