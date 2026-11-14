using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.IntegrationTests;

/// <summary>Stands in for the real AI service, and records what it was asked to do.</summary>
public sealed class FakeAiConnectionTester : IAiConnectionTester
{
    public string? LastKey { get; private set; }
    public AiConnectionResult Result { get; set; } = new(true, "Connected. The model answered.");
    public Task<AiConnectionResult> TestAsync(string baseUrl, string model, string apiKey, CancellationToken ct = default)
    {
        LastKey = apiKey;
        return Task.FromResult(Result);
    }
}

public sealed class AssistantApiFactory : ApiFactory
{
    public FakeAiConnectionTester Tester { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IAiConnectionTester>();
            s.AddSingleton<IAiConnectionTester>(Tester);
        });
    }
}

/// <summary>The founder's control of the AI assistant: who can change it, and that a key never comes back out.</summary>
public class AiAssistantConsoleTests(AssistantApiFactory f) : IClassFixture<AssistantApiFactory>
{
    private const string Url = "/api/v1/platform-admin/assistant";
    private const string SecretKey = "test-key-abcdefghijklmnop-WXYZ";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static object Input(string name, string? key = SecretKey, string baseUrl = "https://api.example.com/v1", string model = "model-a", bool brand = true, bool workspace = false)
        => new { name, baseUrl, model, apiKey = key, useForBrand = brand, useForWorkspace = workspace };

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private Task<(HttpClient Client, JsonObject Session)> FounderAsync() => f.SignInAsync(ApiFactory.FounderEmail);

    private async Task<(JsonNode Provider, JsonNode View)> AddAsync(HttpClient founder, string name, string? key = SecretKey, bool brand = true, bool workspace = false)
    {
        var view = await Ok(await founder.PostAsJsonAsync($"{Url}/providers", Input(name, key, brand: brand, workspace: workspace)));
        return (view["providers"]!.AsArray().Single(p => (string)p!["name"]! == name)!, view);
    }

    private async Task<HttpClient> TeamMemberAsync(HttpClient founder, string role = "manager")
    {
        var tag = Tag();
        var added = await Ok(await founder.PostAsJsonAsync("/api/v1/platform-admin/team", new { email = $"manager+{tag}@example.test", fullName = "Manager", role }));
        var (client, _) = await f.SignInAsync($"manager+{tag}@example.test", (string)added["temporaryPassword"]!);
        return client;
    }

    [Fact]
    public async Task A_stored_key_is_encrypted_and_never_comes_back()
    {
        var (founder, _) = await FounderAsync();
        var name = "Never " + Tag();
        var (provider, _) = await AddAsync(founder, name);
        Assert.Equal("WXYZ", (string)provider["keyHint"]!);
        Assert.False((bool)provider["keyNeedsReentry"]!);

        var text = await (await founder.GetAsync(Url)).Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretKey, text);
        Assert.DoesNotContain("encryptedApiKey", text, StringComparison.OrdinalIgnoreCase);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.AiProviders.AsNoTracking().SingleAsync(p => p.Name == name);
        Assert.DoesNotContain(SecretKey, row.EncryptedApiKey);
        Assert.False(await db.AuditEvents.IgnoreQueryFilters().AnyAsync(a => a.RecordId == row.Id.ToString()));
        var activity = await db.PlatformActivities.AsNoTracking().Where(a => a.Detail != null && a.Detail.Contains(name)).ToListAsync();
        Assert.NotEmpty(activity);
        Assert.All(activity, a => Assert.DoesNotContain(SecretKey, a.Detail!));
    }

    [Fact]
    public async Task Only_the_owner_can_change_anything()
    {
        var (founder, _) = await FounderAsync();
        var (provider, _) = await AddAsync(founder, "Owner " + Tag());
        var id = (string)provider["id"]!;
        var manager = await TeamMemberAsync(founder);

        var seen = await Ok(await manager.GetAsync(Url));
        Assert.False((bool)seen["canManage"]!);
        Assert.True((bool)(await Ok(await founder.GetAsync(Url)))["canManage"]!);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"{Url}/providers", Input("Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"{Url}/providers/{id}", Input("Nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"{Url}/providers/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"{Url}/providers/{id}/test", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = true, workspaceEnabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"{Url}/providers/order", new { ids = new[] { id } })).StatusCode);
    }

    [Fact]
    public async Task Nobody_outside_the_platform_team_can_look()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync(Url)).StatusCode);
        var (client, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task The_owner_cannot_be_removed_from_the_team()
    {
        var (founder, _) = await FounderAsync();
        var manager = await TeamMemberAsync(founder);
        var me = await Ok(await founder.GetAsync("/api/v1/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync($"/api/v1/platform-admin/team/{me["user"]!["id"]}")).StatusCode);
        // A support member may not change the team at all.
        var support = await TeamMemberAsync(founder, "support");
        Assert.Equal(HttpStatusCode.Forbidden, (await support.DeleteAsync($"/api/v1/platform-admin/team/{me["user"]!["id"]}")).StatusCode);
    }

    [Theory]
    [InlineData("http://api.example.com/v1")]
    [InlineData("https://localhost/v1")]
    [InlineData("https://127.0.0.1/v1")]
    [InlineData("https://192.168.1.10/v1")]
    [InlineData("https://10.0.0.5/v1")]
    [InlineData("https://169.254.169.254/latest")]
    [InlineData("https://[::1]/v1")]
    [InlineData("https://intranet/v1")]
    [InlineData("https://user:pass@api.example.com/v1")]
    public async Task A_key_can_only_be_sent_to_a_public_secure_address(string baseUrl)
    {
        var (founder, _) = await FounderAsync();
        var res = await founder.PostAsJsonAsync($"{Url}/providers", Input("Bad", baseUrl: baseUrl));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("baseUrl", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_new_service_needs_a_key()
    {
        var (founder, _) = await FounderAsync();
        var res = await founder.PostAsJsonAsync($"{Url}/providers", Input("No key", key: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("apiKey", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Editing_keeps_the_key_unless_the_address_changes()
    {
        var (founder, _) = await FounderAsync();
        var name = "Edit " + Tag();
        var (provider, _) = await AddAsync(founder, name);
        var id = (string)provider["id"]!;

        var moved = await founder.PutAsJsonAsync($"{Url}/providers/{id}", Input(name, key: null, baseUrl: "https://other.example.com/v1"));
        Assert.Equal(HttpStatusCode.BadRequest, moved.StatusCode);
        Assert.Contains("apiKey", await moved.Content.ReadAsStringAsync());

        await Ok(await founder.PutAsJsonAsync($"{Url}/providers/{id}", Input(name, key: null, model: "model-b")));
        await Ok(await founder.PostAsync($"{Url}/providers/{id}/test", null));
        Assert.Equal(SecretKey, f.Tester.LastKey);

        var replaced = await Ok(await founder.PutAsJsonAsync($"{Url}/providers/{id}", Input(name, key: "replacement-key-1234", baseUrl: "https://other.example.com/v1")));
        var row = replaced["providers"]!.AsArray().Single(p => (string)p!["id"]! == id)!;
        Assert.Equal("1234", (string)row["keyHint"]!);
        Assert.Null(row["lastTestOk"]);
    }

    [Fact]
    public async Task A_test_records_its_result_and_uses_the_stored_key()
    {
        var (founder, _) = await FounderAsync();
        var name = "Test " + Tag();
        var (provider, _) = await AddAsync(founder, name);
        var id = (string)provider["id"]!;

        f.Tester.Result = new(false, "The service rejected the key.");
        var failed = (await Ok(await founder.PostAsync($"{Url}/providers/{id}/test", null)))["providers"]!.AsArray().Single(p => (string)p!["id"]! == id)!;
        Assert.False((bool)failed["lastTestOk"]!);
        Assert.Equal("The service rejected the key.", (string)failed["lastTestMessage"]!);
        Assert.Equal(SecretKey, f.Tester.LastKey);
        f.Tester.Result = new(true, "Connected. The model answered.");
    }

    [Fact]
    public async Task A_key_that_can_no_longer_be_read_asks_to_be_pasted_again()
    {
        var (founder, _) = await FounderAsync();
        var name = "Lost " + Tag();
        var (provider, _) = await AddAsync(founder, name);
        var id = (string)provider["id"]!;
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AiProviders.SingleAsync(p => p.Name == name)).EncryptedApiKey = "not-a-readable-value";
            await db.SaveChangesAsync();
        }
        var row = (await Ok(await founder.PostAsync($"{Url}/providers/{id}/test", null)))["providers"]!.AsArray().Single(p => (string)p!["id"]! == id)!;
        Assert.True((bool)row["keyNeedsReentry"]!);
        Assert.False((bool)row["lastTestOk"]!);
    }

    [Fact]
    public async Task Services_keep_the_order_the_founder_sets_and_can_be_deleted()
    {
        var (founder, _) = await FounderAsync();
        var a = "Order A " + Tag();
        var b = "Order B " + Tag();
        await AddAsync(founder, a);
        var (_, view) = await AddAsync(founder, b);
        var ids = view["providers"]!.AsArray().Select(p => (string)p!["id"]!).ToList();
        ids.Reverse();

        var names = (await Ok(await founder.PutAsJsonAsync($"{Url}/providers/order", new { ids })))["providers"]!.AsArray().Select(p => (string)p!["name"]!).ToList();
        Assert.True(names.IndexOf(b) < names.IndexOf(a));
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PutAsJsonAsync($"{Url}/providers/order", new { ids = ids.Skip(1) })).StatusCode);

        var id = ids[0];
        var left = await Ok(await founder.DeleteAsync($"{Url}/providers/{id}"));
        Assert.DoesNotContain(left["providers"]!.AsArray(), p => (string)p!["id"]! == id);
        using var scope = f.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AiProviders.AnyAsync(p => p.Id == Guid.Parse(id)));
    }

    [Fact]
    public async Task An_assistant_can_only_be_shown_with_a_service_behind_it()
    {
        var (founder, _) = await FounderAsync();
        var view = await Ok(await founder.GetAsync(Url));
        foreach (var p in view["providers"]!.AsArray()) await founder.DeleteAsync($"{Url}/providers/{p!["id"]}");
        await Ok(await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = false, workspaceEnabled = false }));

        var refused = await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = true, workspaceEnabled = false });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("brandEnabled", await refused.Content.ReadAsStringAsync());

        await AddAsync(founder, "Show " + Tag());
        Assert.True((bool)(await Ok(await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = true, workspaceEnabled = false })))["brandEnabled"]!);

        var noClientData = await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = true, workspaceEnabled = true });
        Assert.Equal(HttpStatusCode.BadRequest, noClientData.StatusCode);
        Assert.Contains("workspaceEnabled", await noClientData.Content.ReadAsStringAsync());

        await AddAsync(founder, "Cleared " + Tag(), workspace: true);
        Assert.True((bool)(await Ok(await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = true, workspaceEnabled = true })))["workspaceEnabled"]!);
        await Ok(await founder.PutAsJsonAsync($"{Url}/visibility", new { brandEnabled = false, workspaceEnabled = false }));
    }
}

/// <summary>Who owns the platform, across restarts. Production normally starts without Platform:FounderEmail, so this must not depend on that setting always being there.</summary>
public class PlatformOwnerStartupTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static IConfiguration Config(string? founderEmail) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:FounderEmail"] = founderEmail }).Build();

    private async Task<Guid?> RestartAsync(string? founderEmail, bool clearOwner = false)
    {
        using var scope = f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        if (clearOwner)
        {
            (await db.PlatformSettings.OrderBy(s => s.CreatedAt).FirstAsync()).OwnerUserId = null;
            await db.SaveChangesAsync();
        }
        await new PlatformBootstrap(db, sp.GetRequiredService<UserManager<ERP.Infrastructure.Identity.AppUser>>(), sp.GetRequiredService<IOnboardingService>(), Config(founderEmail), NullLogger<PlatformBootstrap>.Instance).RunAsync();
        db.ChangeTracker.Clear();
        return (await db.PlatformSettings.AsNoTracking().OrderBy(s => s.CreatedAt).FirstAsync()).OwnerUserId;
    }

    [Fact]
    public async Task The_owner_survives_restarts_and_is_only_guessed_when_there_is_one_candidate()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var founderId = Guid.Parse((string)(await ApiFactory.Json(await founder.GetAsync("/api/v1/me")))["user"]!["id"]!);

        Assert.Equal(founderId, await RestartAsync(null));
        Assert.Equal(founderId, await RestartAsync(null, clearOwner: true));

        var added = await ApiFactory.Json(await founder.PostAsJsonAsync("/api/v1/platform-admin/team", new { email = $"second+{Guid.NewGuid():N}@example.test", fullName = "Second" }));
        Assert.NotNull(added["member"]);
        Assert.Null(await RestartAsync(null, clearOwner: true));
        Assert.Equal(founderId, await RestartAsync(ApiFactory.FounderEmail));
    }
}
