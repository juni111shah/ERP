using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Assistant;
using ERP.Application.Platform;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>The public assistant, with the AI service scripted: what a stranger can do, what they can never reach, and what happens when the service is gone.</summary>
[Collection("assistant-engine")]
public class BrandAssistantTests(EngineApiFactory f) : IClassFixture<EngineApiFactory>
{
    private const string Public = "/api/v1/public/assistant";
    private const string Admin = "/api/v1/platform-admin";
    private const string Service = "https://brand.example.com/v1";

    private static IEnumerable<ChatStreamEvent> Reply(string text) => [new ChatTextDelta(text), new ChatUsage(10, 5)];

    private async Task<HttpClient> ResetAsync(bool show = true, bool addService = true)
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AiProviders.RemoveRange(db.AiProviders);
            db.AssistantUsageDays.RemoveRange(db.AssistantUsageDays);
            db.BrandMessages.RemoveRange(db.BrandMessages);
            db.BrandConversations.RemoveRange(db.BrandConversations);
            db.BrandLeads.RemoveRange(db.BrandLeads);
            var s = await db.PlatformSettings.OrderBy(x => x.CreatedAt).FirstAsync();
            s.BrandAssistantDailyTokens = 1_000_000;
            s.BrandAssistantEnabled = false;
            s.SupportEmail = "help@example.test";
            await db.SaveChangesAsync();
        }
        if (f.Services.GetRequiredService<IMemoryCache>() is MemoryCache cache) cache.Clear();
        f.Chat.Scripts.Clear();
        f.Chat.Calls.Clear();
        if (addService)
        {
            f.Tester.Result = new(true, "Connected. The model answered.");
            await EngineApiFactoryBase.AddLiveServiceAsync(founder, "Brand", Service);
            if (show) (await founder.PutAsJsonAsync($"{Admin}/assistant/visibility", new { brandEnabled = true, workspaceEnabled = false })).EnsureSuccessStatusCode();
        }
        return founder;
    }

    private static object Ask(string text, string? token = null, string? website = null) => new { token, text, website };

    private static async Task<List<(string Name, JsonNode Data)>> ReadAsync(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, text);
        return text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries).Where(b => b.StartsWith("event:")).Select(b =>
        {
            var lines = b.Split('\n');
            return (lines[0]["event:".Length..].Trim(), JsonNode.Parse(lines[1]["data:".Length..])!);
        }).ToList();
    }

    private static string Written(IEnumerable<(string Name, JsonNode Data)> events) => string.Concat(events.Where(e => e.Name == "delta").Select(e => (string)e.Data["text"]!));
    private static string TokenOf(IEnumerable<(string Name, JsonNode Data)> events) => (string)events.Single(e => e.Name == "session").Data["token"]!;

    [Fact]
    public async Task It_stays_hidden_and_closed_until_the_founder_turns_it_on()
    {
        await ResetAsync(show: false);
        var status = await ApiFactory.Json(await f.Client().GetAsync($"{Public}/status"));
        Assert.False((bool)status["visible"]!);

        Assert.Equal(HttpStatusCode.NotFound, (await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "A", email = "a@example.test", consent = true })).StatusCode);
        Assert.Empty(f.Chat.Calls);
    }

    [Fact]
    public async Task A_visitor_gets_a_streamed_reply_and_never_learns_which_service_answered()
    {
        await ResetAsync();
        f.Chat.Script(Service, _ => Reply("Hello there"));

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello")));

        Assert.Equal("Hello there", Written(events));
        Assert.DoesNotContain(events, e => e.Name == "service");
        Assert.NotEmpty(TokenOf(events));
    }

    [Fact]
    public async Task The_model_is_given_live_public_facts_and_the_rules()
    {
        var founder = await ResetAsync();
        var settings = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["aboutText"] = "We build software in Dubai.";
        (await founder.PutAsJsonAsync($"{Admin}/settings", settings)).EnsureSuccessStatusCode();
        f.Chat.Script(Service, _ => Reply("Hi"));

        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("How does the free trial work?")));

        var system = f.Chat.Calls.Single().Request.System;
        Assert.Contains("Never make up or change a price", system);
        Assert.Contains("We build software in Dubai.", system);
        Assert.Contains("Starter", system);
        Assert.Contains("How does the free trial work?", system); // the matching written answer
        Assert.Contains("help@example.test", system);
        Assert.DoesNotContain("Reply with", system);
    }

    [Fact]
    public async Task A_price_question_gets_cards_from_the_price_list_not_from_the_model()
    {
        await ResetAsync();
        f.Chat.Script(Service, _ => Reply("Here are our plans."), _ => Reply("I see."));

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("How much does it cost?")));
        var plans = events.Single(e => e.Name == "plans").Data["plans"]!.AsArray();
        Assert.Contains(plans, p => (string)p!["code"]! == "starter");
        Assert.DoesNotContain(events.Single(e => e.Name == "plans").Data.ToJsonString(), "clientCount");

        var other = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Tell me about your company")));
        Assert.DoesNotContain(other, e => e.Name == "plans");
    }

    [Fact]
    public async Task The_plan_recommendation_comes_from_code()
    {
        await ResetAsync();
        var result = "";
        f.Chat.Script(Service, _ => [new ChatToolCallReady(new ChatToolCall("c1", "recommend_plan", """{"users":12,"legal_entities":2}""")), new ChatUsage(5, 1)], r =>
        {
            result = r.Messages.Last(m => m.Role == ChatRole.Tool).Text!;
            return Reply("Business fits.");
        });

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("We have 12 users and 2 companies")));

        Assert.Equal("Business fits.", Written(events));
        Assert.Contains("Business", result);
        Assert.Contains("up to 25 users", result);
    }

    [Fact]
    public async Task A_conversation_continues_only_with_its_own_signed_token()
    {
        await ResetAsync();
        f.Chat.Script(Service, _ => Reply("First answer"), r =>
        {
            Assert.Contains(r.Messages, m => m.Text == "First answer");
            return Reply("Second answer");
        }, r =>
        {
            Assert.DoesNotContain(r.Messages, m => m.Text == "First answer");
            return Reply("Fresh start");
        });

        var first = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("One")));
        Assert.Equal("Second answer", Written(await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Two", TokenOf(first))))));
        // A made-up token cannot reach anyone's chat. It simply starts a new one.
        Assert.Equal("Fresh start", Written(await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Three", "not-a-real-token")))));
    }

    [Fact]
    public async Task A_long_conversation_ends_with_an_offer_to_talk_to_a_person()
    {
        await ResetAsync();
        var first = await SeedConversationAsync(20);
        var calls = f.Chat.Calls.Count;

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("One more", first)));

        Assert.Contains(events, e => e.Name == "contact");
        Assert.Equal(calls, f.Chat.Calls.Count);
    }

    private async Task<string> SeedConversationAsync(int visitorMessages)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conversation = new BrandConversation { StartedAt = DateTime.UtcNow, LastMessageAt = DateTime.UtcNow, VisitorMessages = visitorMessages };
        db.BrandConversations.Add(conversation);
        await db.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<IBrandSessionTokens>().Issue(conversation.Id);
    }

    [Fact]
    public async Task With_no_service_left_the_visitor_still_gets_the_written_answer_and_the_contact_offer()
    {
        var founder = await ResetAsync();
        f.Chat.Script(Service, _ => throw new ChatServiceException(ChatFailureKind.Unavailable, "secret internal detail"));

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("How do I pay?")));

        Assert.Contains("bank transfer", Written(events));
        Assert.Contains("help@example.test", Written(events));
        Assert.Contains(events, e => e.Name == "contact");
        Assert.DoesNotContain(events, e => e.Name == "error");
        Assert.DoesNotContain(events, e => e.Data.ToJsonString().Contains("secret internal detail"));
        Assert.NotNull(founder);
    }

    [Fact]
    public async Task At_the_daily_limit_it_answers_in_arabic_from_the_written_answers_without_calling_a_service()
    {
        var founder = await ResetAsync();
        (await founder.PutAsJsonAsync($"{Admin}/assistant/limits", new { brandDailyTokens = 1000, workspaceDailyTokens = 1000, workspaceDailyMessagesPerClient = 200 })).EnsureSuccessStatusCode();
        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IAssistantUsageStore>().RecordAsync(DateOnly.FromDateTime(DateTime.UtcNow), AssistantBot.Brand, Guid.NewGuid(), "Used", 2000, 0, null);
        var calls = f.Chat.Calls.Count;

        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("كيف أدفع؟")));

        Assert.Contains("تحويل بنكي", Written(events));
        Assert.Equal(calls, f.Chat.Calls.Count);
    }

    [Fact]
    public async Task A_bot_that_fills_the_hidden_field_spends_nothing()
    {
        await ResetAsync();
        var events = await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello", website: "https://spam.example")));
        Assert.Equal("done", events.Single().Name);
        Assert.Empty(f.Chat.Calls);
    }

    [Fact]
    public async Task A_message_that_is_too_long_is_refused_with_the_normal_error()
    {
        await ResetAsync();
        var res = await f.Client().PostAsJsonAsync($"{Public}/messages", Ask(new string('x', 1001)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("\"success\":false", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_reply_quoting_a_price_that_matches_no_plan_is_marked_for_review()
    {
        var founder = await ResetAsync();
        f.Chat.Script(Service, _ => Reply("It costs USD 12.34 per month."));

        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Tell me something")));

        var row = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/assistant/conversations"))).AsArray().Single()!;
        Assert.True((bool)row["needsReview"]!);
        Assert.Equal("Tell me something", (string)row["firstMessage"]!);
        var transcript = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/assistant/conversations/{row["id"]}"))).AsArray();
        Assert.Equal(2, transcript.Count);
    }

    private async Task<HttpClient> TeamMemberAsync(HttpClient founder)
    {
        var added = await ApiFactory.Json(await founder.PostAsJsonAsync($"{Admin}/team", new { email = $"team+{Guid.NewGuid():N}@example.test", fullName = "Team" }));
        return (await f.SignInAsync((string)added["member"]!["email"]!, (string)added["temporaryPassword"]!)).Client;
    }

    [Fact]
    public async Task A_flagged_chat_can_be_marked_reviewed_and_deleted_by_the_owner_only()
    {
        var founder = await ResetAsync();
        var manager = await TeamMemberAsync(founder);
        f.Chat.Script(Service, _ => Reply("A special price of $99."));
        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Any deals?")));
        (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "Keep", email = "keep@example.test", consent = true })).EnsureSuccessStatusCode();
        var chats = $"{Admin}/assistant/conversations";
        var id = (string)(await ApiFactory.Json(await founder.GetAsync(chats))).AsArray().Single()!["id"]!;
        Assert.True((bool)(await ApiFactory.Json(await manager.GetAsync(chats))).AsArray().Single()!["needsReview"]!);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"{chats}/{id}/reviewed", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"{chats}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsync($"{chats}/{id}/reviewed", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await founder.PostAsync($"{chats}/{Guid.NewGuid()}/reviewed", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await founder.PostAsync($"{chats}/{id}/reviewed", null)).StatusCode);
        Assert.False((bool)(await ApiFactory.Json(await founder.GetAsync(chats))).AsArray().Single()!["needsReview"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await founder.DeleteAsync($"{chats}/{id}")).StatusCode);
        Assert.Empty((await ApiFactory.Json(await founder.GetAsync(chats))).AsArray());
        Assert.Empty((await ApiFactory.Json(await founder.GetAsync($"{chats}/{id}"))).AsArray());
        Assert.Single((await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads"))).AsArray());
    }

    [Fact]
    public async Task New_leads_are_counted_for_the_console_until_someone_handles_them()
    {
        var founder = await ResetAsync();
        Assert.Equal(0, (int)(await ApiFactory.Json(await founder.GetAsync($"{Admin}/dashboard")))["newLeads"]!);
        (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "One", email = "one@example.test", consent = true })).EnsureSuccessStatusCode();
        (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "Two", email = "two@example.test", consent = true })).EnsureSuccessStatusCode();
        Assert.Equal(2, (int)(await ApiFactory.Json(await founder.GetAsync($"{Admin}/dashboard")))["newLeads"]!);

        var id = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads"))).AsArray()[0]!["id"];
        (await founder.PutAsync($"{Admin}/leads/{id}/status/contacted", null)).EnsureSuccessStatusCode();
        Assert.Equal(1, (int)(await ApiFactory.Json(await founder.GetAsync($"{Admin}/dashboard")))["newLeads"]!);
    }

    [Fact]
    public async Task Leads_can_be_filtered_searched_and_paged()
    {
        var founder = await ResetAsync();
        foreach (var name in new[] { "Amal", "Badr", "Carim" })
            (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name, email = $"{name.ToLowerInvariant()}@example.test", consent = true })).EnsureSuccessStatusCode();
        async Task<List<string>> Names(string query) => (await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads?{query}"))).AsArray().Select(l => (string)l!["name"]!).ToList();

        var badr = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads?search=badr"))).AsArray().Single()!;
        (await founder.PutAsync($"{Admin}/leads/{badr["id"]}/status/won", null)).EnsureSuccessStatusCode();

        Assert.Equal(["Badr"], await Names("status=won"));
        Assert.Equal(2, (await Names("status=new")).Count);
        Assert.Equal(["Amal"], await Names("status=new&search=amal"));
        Assert.Empty(await Names("status=won&search=amal"));

        var pages = (await Names("page=1&pageSize=2")).Concat(await Names("page=2&pageSize=2")).ToList();
        Assert.Equal(3, pages.Count);
        Assert.Equal(3, pages.Distinct().Count());
    }

    private async Task<JsonNode> ServiceAsync(HttpClient founder) => (await ApiFactory.Json(await founder.GetAsync($"{Admin}/assistant")))["providers"]!.AsArray().Single()!;
    private static Func<ChatRequest, IEnumerable<ChatStreamEvent>> Failing(ChatFailureKind kind, string message) => _ => throw new ChatServiceException(kind, message);

    [Fact]
    public async Task A_rejected_key_shows_as_failed_at_once_until_a_test_passes()
    {
        var founder = await ResetAsync();
        Assert.True((bool)(await ServiceAsync(founder))["lastTestOk"]!);
        f.Chat.Script(Service, Failing(ChatFailureKind.KeyRejected, "The service rejected the key."));

        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello")));

        var service = await ServiceAsync(founder);
        Assert.False((bool)service["lastTestOk"]!);
        Assert.Equal("The service rejected the key.", (string)service["lastTestMessage"]!);
        Assert.True((bool)service["useForBrand"]!); // still in use: the visitor falls back to written answers, and the founder decides

        var passed = await ApiFactory.Json(await founder.PostAsync($"{Admin}/assistant/providers/{service["id"]}/test", null));
        Assert.True((bool)passed["providers"]![0]!["lastTestOk"]!);
    }

    [Fact]
    public async Task Failing_again_and_again_shows_as_failed_and_a_real_answer_clears_it()
    {
        var founder = await ResetAsync();
        var down = Failing(ChatFailureKind.Unavailable, "Could not reach the service.");
        f.Chat.Script(Service, down, down, down, _ => Reply("Back again"));
        async Task AskOnce() => await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello")));

        await AskOnce();
        await AskOnce();
        Assert.True((bool)(await ServiceAsync(founder))["lastTestOk"]!); // two in a row can be bad luck
        await AskOnce();
        var failed = await ServiceAsync(founder);
        Assert.False((bool)failed["lastTestOk"]!);
        Assert.Equal("Could not reach the service.", (string)failed["lastTestMessage"]!);

        await AskOnce();
        Assert.True((bool)(await ServiceAsync(founder))["lastTestOk"]!);
    }

    [Fact]
    public async Task A_service_at_its_reported_limit_does_not_show_as_failed()
    {
        var founder = await ResetAsync();
        f.Chat.Script(Service, Failing(ChatFailureKind.RateLimited, "Limit."));

        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Hello")));

        Assert.True((bool)(await ServiceAsync(founder))["lastTestOk"]!);
    }

    [Fact]
    public async Task A_lead_reaches_the_console_and_needs_consent()
    {
        var founder = await ResetAsync();
        var visitor = f.Client();

        var noConsent = await visitor.PostAsJsonAsync($"{Public}/leads", new { name = "Sara", email = "sara@example.test", consent = false });
        Assert.Equal(HttpStatusCode.BadRequest, noConsent.StatusCode);
        var badEmail = await visitor.PostAsJsonAsync($"{Public}/leads", new { name = "Sara", email = "not-an-email", consent = true });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);

        var sent = await visitor.PostAsJsonAsync($"{Public}/leads", new { name = "Sara", email = "Sara@Example.test", phone = "+97150", company = "Acme", need = "A demo", consent = true });
        Assert.Equal(HttpStatusCode.NoContent, sent.StatusCode);

        var lead = (await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads"))).AsArray().Single()!;
        Assert.Equal("sara@example.test", (string)lead["email"]!);
        Assert.Equal("new", (string)lead["status"]!);
        Assert.False((bool)lead["hasRegistered"]!);

        var changed = await ApiFactory.Json(await founder.PutAsync($"{Admin}/leads/{lead["id"]}/status/contacted", null));
        Assert.Equal("contacted", (string)changed["status"]!);

        Assert.Equal(HttpStatusCode.NoContent, (await founder.DeleteAsync($"{Admin}/leads/{lead["id"]}")).StatusCode);
        Assert.Empty((await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads"))).AsArray());
    }

    [Fact]
    public async Task A_lead_from_a_bot_is_dropped_quietly()
    {
        var founder = await ResetAsync();
        var res = await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "Spam", email = "spam@example.test", consent = true, website = "https://spam.example" });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);
        Assert.Empty((await ApiFactory.Json(await founder.GetAsync($"{Admin}/leads"))).AsArray());
    }

    [Fact]
    public async Task Leads_and_chats_are_for_the_platform_team_only()
    {
        await ResetAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Admin}/leads")).StatusCode);
        var (client, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Admin}/leads")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{Admin}/assistant/conversations")).StatusCode);
    }

    [Fact]
    public async Task Old_chats_are_deleted_and_leads_are_kept()
    {
        var founder = await ResetAsync();
        (await f.Client().PostAsJsonAsync($"{Public}/leads", new { name = "Keep", email = "keep@example.test", consent = true })).EnsureSuccessStatusCode();
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var old = new BrandConversation { StartedAt = DateTime.UtcNow.AddDays(-40), LastMessageAt = DateTime.UtcNow.AddDays(-40) };
            var recent = new BrandConversation { StartedAt = DateTime.UtcNow, LastMessageAt = DateTime.UtcNow };
            db.AddRange(old, recent, new BrandMessage { ConversationId = old.Id, At = old.StartedAt, FromVisitor = true, Text = "Old" });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetServices<IPlatformJob>().Single().RunAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.BrandConversations.CountAsync());
            Assert.Equal(0, await db.BrandMessages.CountAsync());
            Assert.Equal(1, await db.BrandLeads.CountAsync());
        }
        Assert.NotNull(founder);
    }

    [Fact]
    public async Task Chat_text_never_reaches_the_audit_trail()
    {
        await ResetAsync();
        f.Chat.Script(Service, _ => Reply("Audit check reply"));
        await ReadAsync(await f.Client().PostAsJsonAsync($"{Public}/messages", Ask("Audit check question")));

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AuditEvents.IgnoreQueryFilters().AnyAsync(a => a.RecordType == nameof(BrandMessage) || a.RecordType == nameof(BrandConversation)));
    }

    [Fact]
    public async Task The_public_page_content_has_one_source_with_both_languages()
    {
        await ResetAsync();
        var content = await ApiFactory.Json(await f.Client().GetAsync("/api/v1/public/content"));
        Assert.NotEmpty(content["builtIn"]!.AsArray());
        Assert.NotEmpty(content["trust"]!.AsArray());
        var faqs = content["faqs"]!.AsArray();
        Assert.Contains(faqs, q => ((string)q!["q"]!).Contains("free trial"));
        Assert.All(faqs, q => Assert.False(string.IsNullOrWhiteSpace((string?)q!["aAr"])));
        Assert.Contains(faqs, q => ((string)q!["a"]!).Contains("help@example.test"));
    }
}

/// <summary>The rate limit needs its own host, because the other tests turn it up.</summary>
public class BrandAssistantRateLimitTests
{
    private sealed class LimitedFactory : EngineApiFactoryBase
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:AssistantPerHour", "2");
        }
    }

    [Fact]
    public async Task One_address_can_only_ask_so_many_questions_an_hour()
    {
        await using var f = new LimitedFactory();
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        await EngineApiFactoryBase.AddLiveServiceAsync(founder, "Limited", "https://limited.example.com/v1");
        (await founder.PutAsJsonAsync("/api/v1/platform-admin/assistant/visibility", new { brandEnabled = true, workspaceEnabled = false })).EnsureSuccessStatusCode();
        IEnumerable<ChatStreamEvent> Reply(ChatRequest _) => [new ChatTextDelta("Hi")];
        f.Chat.Script("https://limited.example.com/v1", Reply, Reply, Reply);

        var visitor = f.Client();
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/public/assistant/messages", new { text = "One" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await visitor.PostAsJsonAsync("/api/v1/public/assistant/messages", new { text = "Two" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await visitor.PostAsJsonAsync("/api/v1/public/assistant/messages", new { text = "Three" })).StatusCode);
    }
}
