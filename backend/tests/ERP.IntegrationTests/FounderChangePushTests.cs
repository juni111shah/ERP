using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>
/// What the founder (or the daily billing run) changes for a client reaches that client's open screens at once: a suspension, a
/// reactivation, a plan that drops modules, a trial that ends. A change to a platform team member's role reaches theirs.
/// </summary>
public class FounderChangePushTests(PushCaptureFactory f) : IClassFixture<PushCaptureFactory>
{
    private const string Admin = "/api/v1/platform-admin";
    private const string ClientPassword = "Qa#Passw0rd1";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<Guid> NewPlanAsync(HttpClient founder, string[]? modules = null, int trialDays = 14)
    {
        var tag = Tag();
        var plan = await Ok(await founder.PostAsJsonAsync($"{Admin}/plans", new
        {
            code = "p-" + tag, name = "Plan " + tag, description = "Test", isPublic = true, isRecommended = false, sortOrder = 50, monthlyPrice = 100m, annualPrice = 1000m, trialDays,
            maxUsers = (int?)null, maxLegalEntities = (int?)null, modules, features = new[] { "One" },
        }));
        return Guid.Parse((string)plan["id"]!);
    }

    /// <summary>A registered, approved client: its workspace, a signed-in administrator and that person's user id.</summary>
    private async Task<(Guid OrgId, HttpClient Client, string UserId)> NewClientAsync(HttpClient founder, Guid planId)
    {
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["signupMode"] = "review";
        settings["requireEmailVerification"] = false;
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));

        var tag = Tag();
        var email = $"owner+{tag}@example.test";
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", new
        {
            businessName = "Client " + tag, signInKey = "c-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", timeZone = "Asia/Dubai",
            contactName = "Owner " + tag, email, phone = "+971500000000", password = ClientPassword, planId, cycle = "monthly", acceptTerms = true,
        }));
        var signup = (await Ok(await founder.GetAsync($"{Admin}/signups?status=pendingReview&search={tag}"))).AsArray().Single()!;
        var approved = await Ok(await founder.PostAsync($"{Admin}/signups/{signup["id"]}/approve", null));
        var (client, _) = await f.SignInAsync(email, ClientPassword);
        var me = await Ok(await client.GetAsync("/api/v1/me"));
        return (Guid.Parse((string)approved["provisionedOrgId"]!), client, (string)me["user"]!["id"]!);
    }

    private int Count(string userId, string reason) => f.Pushes.AccessChangesFor(userId).Count(p => p.Contains($"\"reason\":\"{reason}\""));

    [Fact]
    public async Task Suspending_and_reactivating_a_client_tells_its_people_and_a_note_does_not()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var (orgId, client, user) = await NewClientAsync(founder, await NewPlanAsync(founder));

        await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgId}/details", new { notes = "Called on Monday" }));
        Assert.Equal(0, Count(user, "subscription-changed"));

        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/suspend", new { reason = "Asked to pause" }));
        Assert.Equal(1, Count(user, "subscription-changed"));
        var pushed = f.Pushes.AccessChangesFor(user).Last();
        Assert.Contains(orgId.ToString(), pushed, StringComparison.OrdinalIgnoreCase);
        // What the client's screens read when the push arrives: the blocked state, with the reason.
        var me = await Ok(await client.GetAsync("/api/v1/me"));
        Assert.Equal("blocked", (string?)me["subscription"]!["access"]);
        Assert.Equal("Asked to pause", (string?)me["subscription"]!["suspendedReason"]);

        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/reactivate", null));
        Assert.Equal(2, Count(user, "subscription-changed"));
        Assert.Equal("full", (string?)(await Ok(await client.GetAsync("/api/v1/me")))["subscription"]!["access"]);
    }

    [Fact]
    public async Task A_plan_that_drops_modules_tells_the_client_and_ends_its_old_tokens()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var (orgId, client, user) = await NewClientAsync(founder, await NewPlanAsync(founder));
        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/modules/crm/enable", null));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);

        var small = await NewPlanAsync(founder, modules: ["inventory"]);
        await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgId}/plan", new { planId = small, cycle = "monthly", isComplimentary = false }));

        Assert.Equal(1, Count(user, "plan-changed"));
        // The old token is refused, so a screen that missed the push still refreshes on its next call...
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        // ...and the silent refresh carries on with the smaller plan.
        var refreshed = await client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (string)(await ApiFactory.Json(refreshed))["accessToken"]!);
        var modules = (await Ok(await client.GetAsync("/api/v1/me")))["enabledModules"]!.AsArray().Select(m => (string)m!["key"]!).ToList();
        Assert.DoesNotContain("crm", modules);
    }

    [Fact]
    public async Task A_trial_ended_by_the_billing_run_tells_the_client()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var (_, _, user) = await NewClientAsync(founder, await NewPlanAsync(founder, trialDays: 5));
        Assert.Equal(0, Count(user, "subscription-changed"));

        using (var scope = f.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycle>().RunAsync(Today.AddDays(6));

        Assert.Equal(1, Count(user, "subscription-changed"));
    }

    [Fact]
    public async Task Changing_a_platform_team_members_role_tells_them()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var email = $"team_{Guid.NewGuid():N}@platform.test";
        var added = await Ok(await founder.PostAsJsonAsync($"{Admin}/team", new { email, fullName = "Team", role = "support" }));
        var id = (string)added["member"]!["userId"]!;
        Assert.Equal(1, Count(id, "platform-team-changed"));

        await Ok(await founder.PutAsJsonAsync($"{Admin}/team/{id}/role", new { role = "manager" }));
        Assert.Equal(2, Count(id, "platform-team-changed"));
        // Not tied to a workspace: every open screen of theirs refreshes.
        Assert.Contains("\"orgId\":null", f.Pushes.AccessChangesFor(id).Last());

        await Ok(await founder.DeleteAsync($"{Admin}/team/{id}"), HttpStatusCode.NoContent);
        Assert.Equal(3, Count(id, "platform-team-changed"));
    }
}
