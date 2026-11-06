using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using ERP.Application.Assistant;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Plays the AI services. A script per service name's address says what each call does.</summary>
public sealed class ScriptedChatClient : IChatModelClient
{
    public Dictionary<string, Queue<Func<ChatRequest, IEnumerable<ChatStreamEvent>>>> Scripts { get; } = [];
    public List<(string BaseUrl, ChatRequest Request)> Calls { get; } = [];

    public void Script(string baseUrl, params Func<ChatRequest, IEnumerable<ChatStreamEvent>>[] steps) => Scripts[baseUrl] = new(steps);

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(AiServiceConnection connection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Calls.Add((connection.BaseUrl, request));
        var step = Scripts[connection.BaseUrl].Dequeue();
        foreach (var e in step(request))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            if (e is WaitUntilCancelled) await Task.Delay(Timeout.Infinite, ct);
            yield return e;
        }
    }
}

/// <summary>Makes the scripted service go quiet, as a slow one does, until the request is cancelled.</summary>
public sealed record WaitUntilCancelled : ChatStreamEvent;

public class EngineApiFactoryBase : ApiFactory
{
    public ScriptedChatClient Chat { get; } = new();
    public FakeAiConnectionTester Tester { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s =>
        {
            s.RemoveAll<IChatModelClient>();
            s.AddSingleton<IChatModelClient>(Chat);
            s.RemoveAll<IAiConnectionTester>();
            s.AddSingleton<IAiConnectionTester>(Tester);
        });
    }

    /// <summary>A service the way the founder brings one into use: added, tested, then turned on.</summary>
    public static async Task AddLiveServiceAsync(HttpClient founder, string name, string baseUrl, bool brand = true, bool workspace = false)
    {
        const string Providers = "/api/v1/platform-admin/assistant/providers";
        var added = await founder.PostAsJsonAsync(Providers, new { name, baseUrl, model = "m", apiKey = "test-key-abcdef-1234", useForBrand = false, useForWorkspace = false });
        Assert.True(added.IsSuccessStatusCode, await added.Content.ReadAsStringAsync());
        var id = (string)(await Json(added))["providers"]!.AsArray().Last(p => (string)p!["name"]! == name)!["id"]!;
        (await founder.PostAsync($"{Providers}/{id}/test", null)).EnsureSuccessStatusCode();
        var on = await founder.PutAsJsonAsync($"{Providers}/{id}", new { name, baseUrl, model = "m", apiKey = (string?)null, useForBrand = brand, useForWorkspace = workspace });
        Assert.True(on.IsSuccessStatusCode, await on.Content.ReadAsStringAsync());
    }
}

public sealed class EngineApiFactory : EngineApiFactoryBase;

/// <summary>The assistant engine: streaming, moving between services, tools and daily limits. The services are scripted, so nothing here needs the internet or a key.</summary>
[Collection("assistant-engine")]
public class AssistantEngineTests(EngineApiFactory f) : IClassFixture<EngineApiFactory>
{
    private const string Url = "/api/v1/platform-admin/assistant";

    private static IEnumerable<ChatStreamEvent> Reply(string text, int input = 10, int output = 5) => [new ChatTextDelta(text), new ChatUsage(input, output)];
    private static IEnumerable<ChatStreamEvent> Fail(ChatFailureKind kind, string message = "It failed.", TimeSpan? retryAfter = null) => throw new ChatServiceException(kind, message, retryAfter);

    /// <summary>A clean slate: no services, nothing used today, generous limits.</summary>
    private async Task<HttpClient> ResetAsync(int brandLimit = 1_000_000)
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AiProviders.RemoveRange(db.AiProviders);
        db.AssistantUsageDays.RemoveRange(db.AssistantUsageDays);
        (await db.PlatformSettings.OrderBy(s => s.CreatedAt).FirstAsync()).BrandAssistantDailyTokens = brandLimit;
        await db.SaveChangesAsync();
        if (f.Services.GetRequiredService<IMemoryCache>() is MemoryCache cache) cache.Clear();
        f.Chat.Scripts.Clear();
        f.Chat.Calls.Clear();
        return founder;
    }

    private static Task AddServiceAsync(HttpClient founder, string name, string baseUrl, bool workspace = false)
        => EngineApiFactoryBase.AddLiveServiceAsync(founder, name, baseUrl, !workspace, workspace);

    private static object Ask(string text = "Hello", string bot = "brand") => new { bot, messages = new[] { new { role = "user", text } } };

    /// <summary>Reads the whole event stream into (event, data) pairs.</summary>
    private static async Task<List<(string Name, JsonNode Data)>> ReadAsync(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.StartsWith("text/event-stream", res.Content.Headers.ContentType!.ToString());
        var text = await res.Content.ReadAsStringAsync();
        return text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Where(b => b.StartsWith("event:")).Select(b =>
        {
            var lines = b.Split('\n');
            return (lines[0]["event:".Length..].Trim(), JsonNode.Parse(lines[1]["data:".Length..])!);
        }).ToList();
    }

    private static string Written(IEnumerable<(string Name, JsonNode Data)> events) => string.Concat(events.Where(e => e.Name == "delta").Select(e => (string)e.Data["text"]!));

    [Fact]
    public async Task A_reply_arrives_as_events_and_is_counted()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "A", "https://a.example.com/v1");
        f.Chat.Script("https://a.example.com/v1", _ => [new ChatTextDelta("Hel"), new ChatTextDelta("lo"), new ChatUsage(40, 2)]);

        var events = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        Assert.Equal("Hello", Written(events));
        Assert.Equal(["status", "service", "delta", "delta", "done"], events.Select(e => e.Name));
        Assert.Equal("A", (string)events[1].Data["name"]!);
        Assert.Equal(40, (int)events[^1].Data["inputTokens"]!);

        var view = await ApiFactory.Json(await founder.GetAsync(Url));
        var row = view["usageToday"]!.AsArray().Single()!;
        Assert.Equal(1, (int)row["requests"]!);
        Assert.Equal(42, (int)row["inputTokens"]! + (int)row["outputTokens"]!);
    }

    [Fact]
    public async Task It_moves_to_the_next_service_when_one_fails_before_saying_anything()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "First", "https://first.example.com/v1");
        await AddServiceAsync(founder, "Second", "https://second.example.com/v1");
        f.Chat.Script("https://first.example.com/v1", _ => Fail(ChatFailureKind.KeyRejected, "The service rejected the key."));
        f.Chat.Script("https://second.example.com/v1", _ => Reply("From second"));

        var events = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        Assert.Equal("From second", Written(events));
        Assert.Equal("Second", (string)events.Single(e => e.Name == "service").Data["name"]!);
        Assert.DoesNotContain(events, e => e.Name == "error");

        var usage = (await ApiFactory.Json(await founder.GetAsync(Url)))["usageToday"]!.AsArray();
        var first = usage.Single(u => (string)u!["providerName"]! == "First")!;
        Assert.Equal(1, (int)first["failures"]!);
        Assert.Equal("The service rejected the key.", (string)first["lastFailure"]!);
    }

    [Fact]
    public async Task A_service_at_its_limit_is_skipped_until_it_has_cooled_down()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Busy", "https://busy.example.com/v1");
        await AddServiceAsync(founder, "Spare", "https://spare.example.com/v1");
        f.Chat.Script("https://busy.example.com/v1", _ => Fail(ChatFailureKind.RateLimited, "Limit.", TimeSpan.FromMinutes(5)));
        f.Chat.Script("https://spare.example.com/v1", _ => Reply("One"), _ => Reply("Two"));

        Assert.Equal("One", Written(await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()))));
        Assert.Equal("Two", Written(await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()))));
        Assert.Single(f.Chat.Calls, c => c.BaseUrl == "https://busy.example.com/v1");
    }

    [Fact]
    public async Task A_failure_after_text_was_shown_is_reported_and_not_answered_twice()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Breaks", "https://breaks.example.com/v1");
        await AddServiceAsync(founder, "Never", "https://never.example.com/v1");
        static IEnumerable<ChatStreamEvent> PartThenFail()
        {
            yield return new ChatTextDelta("Half a sen");
            throw new ChatServiceException(ChatFailureKind.Unavailable, "Lost.");
        }
        f.Chat.Script("https://breaks.example.com/v1", _ => PartThenFail());

        var events = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        Assert.Equal("Half a sen", Written(events));
        Assert.Equal("interrupted", (string)events[^1].Data["code"]!);
        Assert.DoesNotContain(f.Chat.Calls, c => c.BaseUrl == "https://never.example.com/v1");
    }

    [Fact]
    public async Task When_every_service_fails_it_says_so_without_details()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Down", "https://down.example.com/v1");
        f.Chat.Script("https://down.example.com/v1", _ => Fail(ChatFailureKind.Unavailable, "secret internal detail"));

        var events = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        Assert.Equal("unavailable", (string)events[^1].Data["code"]!);
        Assert.DoesNotContain("secret internal detail", (string)events[^1].Data["message"]!);
    }

    [Fact]
    public async Task The_daily_limit_stops_the_assistant_without_calling_a_service()
    {
        var founder = await ResetAsync(brandLimit: 1000);
        await AddServiceAsync(founder, "Limited", "https://limited.example.com/v1");
        f.Chat.Script("https://limited.example.com/v1", _ => Reply("Big", input: 900, output: 200));

        Assert.Equal("Big", Written(await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()))));
        var calls = f.Chat.Calls.Count;

        var events = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));
        Assert.Equal("daily-limit", (string)events.Single().Data["code"]!);
        Assert.Equal(calls, f.Chat.Calls.Count);
    }

    [Fact]
    public async Task A_service_that_reports_no_usage_is_still_counted()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Quiet", "https://quiet.example.com/v1");
        f.Chat.Script("https://quiet.example.com/v1", _ => [new ChatTextDelta(new string('x', 400))]);

        await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        var row = (await ApiFactory.Json(await founder.GetAsync(Url)))["usageToday"]!.AsArray().Single()!;
        Assert.Equal(100, (int)row["outputTokens"]!);
        Assert.True((int)row["inputTokens"]! > 0);
    }

    [Fact]
    public async Task Leaving_part_way_through_a_reply_still_counts()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Long", "https://long.example.com/v1");
        f.Chat.Script("https://long.example.com/v1", _ => [new ChatTextDelta(new string('x', 400)), new ChatTextDelta("never read"), new ChatUsage(1, 1)]);

        using (var scope = f.Services.CreateScope())
        {
            var runner = scope.ServiceProvider.GetRequiredService<AssistantRunner>();
            await foreach (var e in runner.RunAsync(new AssistantTurn(AssistantBot.Brand, "System", [new ChatMessage(ChatRole.User, "Hello")], [])))
                if (e is AssistantDelta) break; // as when the person closes the page
        }

        var row = (await ApiFactory.Json(await founder.GetAsync(Url)))["usageToday"]!.AsArray().Single()!;
        Assert.Equal(100, (int)row["outputTokens"]!);
        Assert.True((int)row["inputTokens"]! > 0);
    }

    [Fact]
    public async Task Closing_the_connection_mid_reply_still_counts_and_causes_no_error()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Slow", "https://slow.example.com/v1");
        f.Chat.Script("https://slow.example.com/v1", _ => [new ChatTextDelta(new string('x', 400)), new WaitUntilCancelled(), new ChatTextDelta("never sent")]);

        // Through the real HTTP path: the runner is mid-step when the person leaves.
        using (var cancel = new CancellationTokenSource())
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Url}/try") { Content = JsonContent.Create(Ask()) };
            using var response = await founder.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(cancel.Token));
            while (await reader.ReadLineAsync(cancel.Token) is { } line && !line.StartsWith("event: delta")) { }
            cancel.Cancel();
        }

        JsonNode? row = null;
        for (var i = 0; i < 100 && row is null; i++)
        {
            await Task.Delay(50);
            row = (await ApiFactory.Json(await founder.GetAsync(Url)))["usageToday"]!.AsArray().SingleOrDefault();
        }
        Assert.NotNull(row);
        Assert.Equal(100, (int)row!["outputTokens"]!);
        Assert.Equal(0, (int)row["failures"]!);
    }

    [Fact]
    public async Task A_service_that_fails_part_way_still_counts_what_it_wrote()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Breaks", "https://breaks.example.com/v1");
        static IEnumerable<ChatStreamEvent> PartThenFail()
        {
            yield return new ChatTextDelta(new string('x', 400));
            throw new ChatServiceException(ChatFailureKind.Unavailable, "Lost.");
        }
        f.Chat.Script("https://breaks.example.com/v1", _ => PartThenFail());

        await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask()));

        var row = (await ApiFactory.Json(await founder.GetAsync(Url)))["usageToday"]!.AsArray().Single()!;
        Assert.Equal(100, (int)row["outputTokens"]!);
        Assert.Equal(1, (int)row["failures"]!);
    }

    [Fact]
    public async Task Client_data_only_goes_to_services_cleared_for_it()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Public only", "https://public.example.com/v1");

        var none = await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask(bot: "workspace")));
        Assert.Equal("no-service", (string)none.Single().Data["code"]!);
        Assert.Empty(f.Chat.Calls);

        await AddServiceAsync(founder, "Cleared", "https://cleared.example.com/v1", workspace: true);
        f.Chat.Script("https://cleared.example.com/v1", _ => Reply("Safe"));
        Assert.Equal("Safe", Written(await ReadAsync(await founder.PostAsJsonAsync($"{Url}/try", Ask(bot: "workspace")))));
        Assert.All(f.Chat.Calls, c => Assert.Equal("https://cleared.example.com/v1", c.BaseUrl));
    }

    [Fact]
    public async Task Only_the_owner_can_try_and_bad_input_gets_the_normal_error_answer()
    {
        var founder = await ResetAsync();
        var added = await ApiFactory.Json(await founder.PostAsJsonAsync("/api/v1/platform-admin/team", new { email = $"try+{Guid.NewGuid():N}@example.test", fullName = "Team" }));
        var (manager, _) = await f.SignInAsync((string)added["member"]!["email"]!, (string)added["temporaryPassword"]!);
        var forbidden = await manager.PostAsJsonAsync($"{Url}/try", Ask());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains("\"success\":false", await forbidden.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Url}/try", Ask())).StatusCode);

        var bad = await founder.PostAsJsonAsync($"{Url}/try", Ask(new string('x', 2001)));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("\"success\":false", await bad.Content.ReadAsStringAsync());
        var endsWithReply = await founder.PostAsJsonAsync($"{Url}/try", new { bot = "brand", messages = new[] { new { role = "assistant", text = "Hi" } } });
        Assert.Equal(HttpStatusCode.BadRequest, endsWithReply.StatusCode);
        var fakeTool = await founder.PostAsJsonAsync($"{Url}/try", new { bot = "brand", messages = new[] { new { role = "tool", text = "Pretend result" }, new { role = "user", text = "Hi" } } });
        Assert.Equal(HttpStatusCode.BadRequest, fakeTool.StatusCode);
    }

    [Fact]
    public async Task The_limits_can_be_changed_by_the_owner_within_sensible_bounds()
    {
        var founder = await ResetAsync();
        var view = await ApiFactory.Json(await founder.PutAsJsonAsync($"{Url}/limits", new { brandDailyTokens = 2000, workspaceDailyTokens = 3000, workspaceDailyMessagesPerClient = 200 }));
        Assert.Equal(2000, (int)view["brandDailyTokens"]!);
        Assert.Equal(3000, (int)view["workspaceDailyTokens"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await founder.PutAsJsonAsync($"{Url}/limits", new { brandDailyTokens = 10, workspaceDailyTokens = 3000, workspaceDailyMessagesPerClient = 200 })).StatusCode);
    }

    private sealed class FakeTool(string name, Func<string> run) : IAssistantTool
    {
        public int Runs { get; private set; }
        public ChatToolDefinition Definition { get; } = new(name, "A test tool", """{"type":"object","properties":{}}""");
        public string StatusText => "Checking";
        public Task<string> RunAsync(string argumentsJson, CancellationToken ct) { Runs++; return Task.FromResult(run()); }
    }

    private async Task<List<AssistantEvent>> RunWithToolsAsync(params IAssistantTool[] tools)
    {
        using var scope = f.Services.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<AssistantRunner>();
        var events = new List<AssistantEvent>();
        await foreach (var e in runner.RunAsync(new AssistantTurn(AssistantBot.Brand, "System", [new ChatMessage(ChatRole.User, "Hello")], tools))) events.Add(e);
        return events;
    }

    private static ChatToolCallReady Call(string id, string name) => new(new ChatToolCall(id, name, "{}"));

    [Fact]
    public async Task A_tool_result_goes_back_to_the_model_for_the_answer()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Tools", "https://tools.example.com/v1");
        f.Chat.Script("https://tools.example.com/v1", _ => [Call("c1", "lookup"), new ChatUsage(5, 1)], _ => Reply("The price is 49"));
        var tool = new FakeTool("lookup", () => "49");

        var events = await RunWithToolsAsync(tool);

        Assert.Equal(1, tool.Runs);
        Assert.Contains(events, e => e is AssistantStatus { Text: "Checking" });
        Assert.Equal("The price is 49", string.Concat(events.OfType<AssistantDelta>().Select(d => d.Text)));
        var second = f.Chat.Calls[1].Request.Messages;
        Assert.Equal(ChatRole.Assistant, second[1].Role);
        Assert.Equal("c1", second[2].ToolCallId);
        Assert.Equal("49", second[2].Text);
        Assert.Equal(new AssistantDone(15, 6), events.OfType<AssistantDone>().Single());
    }

    [Fact]
    public async Task Tool_use_stops_at_the_limit_and_the_model_must_answer()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Loop", "https://loop.example.com/v1");
        IEnumerable<ChatStreamEvent> Again(ChatRequest r) => r.Tools.Count > 0 ? [Call(Guid.NewGuid().ToString(), "lookup")] : Reply("Answer");
        f.Chat.Script("https://loop.example.com/v1", Again, Again, Again, Again, Again, Again);
        var tool = new FakeTool("lookup", () => "data");

        var events = await RunWithToolsAsync(tool);

        Assert.Equal(AssistantRunner.MaxToolCallsPerTurn, tool.Runs);
        Assert.Empty(f.Chat.Calls[^1].Request.Tools);
        Assert.Contains(events, e => e is AssistantDone);
    }

    [Fact]
    public async Task A_failing_or_unknown_tool_does_not_end_the_reply()
    {
        var founder = await ResetAsync();
        await AddServiceAsync(founder, "Odd", "https://odd.example.com/v1");
        f.Chat.Script("https://odd.example.com/v1", _ => [Call("c1", "broken"), Call("c2", "made_up")], _ => Reply("Sorry"));

        var events = await RunWithToolsAsync(new FakeTool("broken", () => throw new InvalidOperationException("secret detail")));

        Assert.Contains(events, e => e is AssistantDone);
        var results = f.Chat.Calls[1].Request.Messages.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.DoesNotContain("secret detail", r.Text));
    }
}

[CollectionDefinition("assistant-engine", DisableParallelization = true)]
public class AssistantEngineCollection;
