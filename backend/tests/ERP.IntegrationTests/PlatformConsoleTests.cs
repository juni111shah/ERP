using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ERP.IntegrationTests;

/// <summary>Registration, the founder's console, plan limits, the subscription gate and the daily billing run, end to end through the API.</summary>
public class PlatformConsoleTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Admin = "/api/v1/platform-admin";
    private const string ClientPassword = "Qa#Passw0rd1";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private Task<(HttpClient Client, JsonObject Session)> FounderAsync() => f.SignInAsync(ApiFactory.FounderEmail);

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private static object Signup(string tag, Guid? planId = null, string? website = null, string password = ClientPassword, string? email = null) => new
    {
        businessName = "Client " + tag, signInKey = "c-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", timeZone = "Asia/Dubai",
        contactName = "Owner " + tag, email = email ?? $"owner+{tag}@example.test", phone = "+971500000000", password, planId, cycle = "monthly", acceptTerms = true, website,
    };

    private static async Task SetSignupModeAsync(HttpClient founder, string mode, bool verify)
    {
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["signupMode"] = mode;
        settings["requireEmailVerification"] = verify;
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
    }

    /// <summary>A plan with the given limits, in the founder's price list.</summary>
    private static async Task<Guid> NewPlanAsync(HttpClient founder, string tag, int? maxUsers = null, int? maxEntities = null, string[]? modules = null, decimal price = 100, int trialDays = 14)
    {
        var plan = await Ok(await founder.PostAsJsonAsync($"{Admin}/plans", new
        {
            code = "p-" + tag, name = "Plan " + tag, description = "Test", isPublic = true, isRecommended = false, sortOrder = 50, monthlyPrice = price, annualPrice = price * 10, trialDays,
            maxUsers, maxLegalEntities = maxEntities, modules, features = new[] { "One" },
        }));
        return Guid.Parse((string)plan["id"]!);
    }

    /// <summary>A registered, approved client: its workspace id and a signed-in administrator.</summary>
    private async Task<(Guid OrgId, HttpClient Client, string Email)> NewClientAsync(HttpClient founder, Guid? planId = null)
    {
        await SetSignupModeAsync(founder, "review", false);
        var tag = Tag();
        var email = $"owner+{tag}@example.test";
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(tag, planId)));
        var signup = (await Ok(await founder.GetAsync($"{Admin}/signups?status=pendingReview&search={tag}"))).AsArray().Single()!;
        var approved = await Ok(await founder.PostAsync($"{Admin}/signups/{signup["id"]}/approve", null));
        var (client, _) = await f.SignInAsync(email, ClientPassword);
        return (Guid.Parse((string)approved["provisionedOrgId"]!), client, email);
    }

    private async Task<LifecycleResult> RunBillingAsync(DateOnly today)
    {
        using var scope = f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISubscriptionLifecycle>().RunAsync(today);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task The_console_is_for_the_platform_team_only()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Admin}/dashboard")).StatusCode);
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail); // administrator of a client workspace, holding "*"
        foreach (var path in new[] { "dashboard", "clients", "plans", "invoices", "signups", "settings", "activity", "team" })
            Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"{Admin}/{path}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"{Admin}/billing-run", null)).StatusCode);

        var (founder, _) = await FounderAsync();
        await Ok(await founder.GetAsync($"{Admin}/dashboard"));
        var me = await Ok(await founder.GetAsync("/api/v1/me"));
        Assert.True((bool)me["platformAdmin"]!);
        Assert.True((bool)me["isInternalWorkspace"]!);
        Assert.False((bool)(await Ok(await admin.GetAsync("/api/v1/me")))["platformAdmin"]!);
    }

    [Fact]
    public async Task A_registration_waits_for_review_then_becomes_a_working_trial()
    {
        var (founder, _) = await FounderAsync();
        await SetSignupModeAsync(founder, "review", false);
        var tag = Tag();
        var result = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(tag)));
        Assert.Equal("pendingReview", (string?)result["status"]);

        // Nothing exists yet, and the password is nowhere to be read.
        var raw = await (await founder.GetAsync($"{Admin}/signups?search={tag}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = $"owner+{tag}@example.test", password = ClientPassword })).StatusCode);

        var row = (await Ok(await founder.GetAsync($"{Admin}/signups?search={tag}"))).AsArray().Single()!;
        var approved = await Ok(await founder.PostAsync($"{Admin}/signups/{row["id"]}/approve", null));
        Assert.Equal("provisioned", (string?)approved["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsync($"{Admin}/signups/{row["id"]}/approve", null)).StatusCode);

        var (client, _) = await f.SignInAsync($"owner+{tag}@example.test", ClientPassword);
        var me = await Ok(await client.GetAsync("/api/v1/me"));
        Assert.Equal("trialing", (string?)me["subscription"]!["status"]);
        Assert.Equal("full", (string?)me["subscription"]!["access"]);
        Assert.False((bool)me["platformAdmin"]!);

        var detail = await Ok(await founder.GetAsync($"{Admin}/clients/{approved["provisionedOrgId"]}"));
        Assert.Equal("trialing", (string?)detail["client"]!["status"]);
        Assert.Equal(1, (int)detail["client"]!["activeUsers"]!);
        Assert.True((bool)detail["members"]!.AsArray().Single()!["isAdministrator"]!);
    }

    [Fact]
    public async Task Open_mode_without_verification_creates_the_workspace_at_once()
    {
        var (founder, _) = await FounderAsync();
        await SetSignupModeAsync(founder, "open", false);
        var tag = Tag();
        var result = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(tag)));
        Assert.Equal("provisioned", (string?)result["status"]);
        Assert.Equal("c-" + tag, (string?)result["signInKey"]);
        await f.SignInAsync($"owner+{tag}@example.test", ClientPassword);
    }

    [Fact]
    public async Task Verification_with_no_email_server_goes_to_a_person_not_straight_through()
    {
        var (founder, _) = await FounderAsync();
        await SetSignupModeAsync(founder, "open", true);
        var result = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(Tag())));
        Assert.Equal("pendingReview", (string?)result["status"]);
    }

    [Fact]
    public async Task Closed_mode_refuses_and_bad_registrations_are_turned_away()
    {
        var (founder, _) = await FounderAsync();
        await SetSignupModeAsync(founder, "closed", false);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(Tag()))).StatusCode);
        await SetSignupModeAsync(founder, "review", false);

        // A hidden field only a bot fills in: told it worked, nothing kept.
        var bot = Tag();
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(bot, website: "http://spam.test")));
        Assert.Empty((await Ok(await founder.GetAsync($"{Admin}/signups?search={bot}"))).AsArray());

        var reserved = Signup(Tag());
        var body = JsonSerializerNode(reserved);
        body["signInKey"] = "platform";
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync("/api/v1/public/signups", body)).StatusCode);

        var noTerms = JsonSerializerNode(Signup(Tag()));
        noTerms["acceptTerms"] = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync("/api/v1/public/signups", noTerms)).StatusCode);

        // An email that has an account needs that account's password.
        var taken = await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(Tag(), email: ApiFactory.AdminEmail, password: "Wrong#Passw0rd1"));
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Contains("already has an account", await taken.Content.ReadAsStringAsync());

        // Weak passwords never wait in the queue.
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(Tag(), password: "weak"))).StatusCode);

        // A private plan cannot be chosen from the public page.
        var plan = await NewPlanAsync(founder, Tag());
        var view = (await Ok(await founder.GetAsync($"{Admin}/plans"))).AsArray().First(p => (string?)p!["id"] == plan.ToString())!;
        await Ok(await founder.PostAsync($"{Admin}/plans/{plan}/archive", null));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(Tag(), plan))).StatusCode);
        Assert.NotNull(view);

        Assert.False((bool)(await Ok(await f.Client().GetAsync("/api/v1/public/sign-in-key-available?key=demo")))["available"]!);
        Assert.False((bool)(await Ok(await f.Client().GetAsync("/api/v1/public/sign-in-key-available?key=hq")))["available"]!);
        Assert.True((bool)(await Ok(await f.Client().GetAsync($"/api/v1/public/sign-in-key-available?key=free-{Tag()}")))["available"]!);
    }

    private static JsonObject JsonSerializerNode(object value) => System.Text.Json.JsonSerializer.SerializeToNode(value)!.AsObject();

    [Fact]
    public async Task Rejecting_a_registration_discards_it()
    {
        var (founder, _) = await FounderAsync();
        await SetSignupModeAsync(founder, "review", false);
        var tag = Tag();
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Signup(tag)));
        var row = (await Ok(await founder.GetAsync($"{Admin}/signups?search={tag}"))).AsArray().Single()!;
        var rejected = await Ok(await founder.PostAsJsonAsync($"{Admin}/signups/{row["id"]}/reject", new { reason = "Duplicate" }));
        Assert.Equal("rejected", (string?)rejected["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsync($"{Admin}/signups/{row["id"]}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = $"owner+{tag}@example.test", password = ClientPassword })).StatusCode);
        // The code is free again.
        Assert.True((bool)(await Ok(await f.Client().GetAsync($"/api/v1/public/sign-in-key-available?key=c-{tag}")))["available"]!);
    }

    [Fact]
    public async Task A_founder_can_create_a_client_directly_and_the_temporary_password_works()
    {
        var (founder, _) = await FounderAsync();
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["taxPercent"] = 0m;
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
        var plan = await NewPlanAsync(founder, Tag());
        var tag = Tag();
        var created = await Ok(await founder.PostAsJsonAsync($"{Admin}/clients", new
        {
            workspaceName = "Direct " + tag, signInKey = "d-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", timeZone = "Asia/Dubai",
            adminName = "Direct Admin", adminEmail = $"direct+{tag}@example.test", phone = "+97150", planId = plan, cycle = "annual", startMode = "active", customPrice = 900m, notes = "Signed in person",
        }));
        var password = (string?)created["temporaryPassword"];
        Assert.False(string.IsNullOrEmpty(password));
        var (client, _) = await f.SignInAsync($"direct+{tag}@example.test", password!);
        var me = await Ok(await client.GetAsync("/api/v1/me"));
        Assert.True((bool)me["user"]!["mustChangePassword"]!);
        Assert.Equal("active", (string?)me["subscription"]!["status"]);

        // A paid start bills its first period, at the agreed price.
        var detail = await Ok(await founder.GetAsync($"{Admin}/clients/{created["orgId"]}"));
        Assert.Equal("Signed in person", (string?)detail["notes"]);
        Assert.Equal(900m, (decimal)detail["client"]!["cyclePrice"]!);
        Assert.Equal(75m, (decimal)detail["client"]!["monthlyValue"]!);
        var invoice = detail["invoices"]!.AsArray().Single()!;
        Assert.Equal(900m, (decimal)invoice["total"]!);
        Assert.Equal("open", (string?)invoice["status"]);

        // The email is reused when it already has an account: no new password is made for it.
        var again = await Ok(await founder.PostAsJsonAsync($"{Admin}/clients", new
        {
            workspaceName = "Second " + tag, signInKey = "e-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", adminName = "Direct Admin",
            adminEmail = $"direct+{tag}@example.test", planId = plan, cycle = "monthly", startMode = "complimentary",
        }));
        Assert.Null(again["temporaryPassword"]);
    }

    [Fact]
    public async Task Assigning_a_first_plan_as_paid_bills_it_and_an_old_invoice_keeps_its_tax_label()
    {
        var (founder, _) = await FounderAsync();
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["taxLabel"] = "VAT";
        settings["taxPercent"] = 0m;
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));

        // A workspace from before billing existed, with no plan yet.
        var tag = Tag();
        var plan = await NewPlanAsync(founder, Tag(), price: 300);
        using var scope = f.Services.CreateScope();
        var created = await scope.ServiceProvider.GetRequiredService<IOnboardingService>().CreateWorkspaceAsync(
            new OnboardingRequest("Older " + tag, "older-" + tag, "generic", "AE", "AED", null, null, $"older+{tag}@example.test", "Older", ClientPassword));
        Assert.Null((await Ok(await founder.GetAsync($"{Admin}/clients/{created.OrgId}")))["client"]!["status"]);

        var detail = await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{created.OrgId}/plan", new { planId = plan, cycle = "monthly", isComplimentary = false, startMode = "active" }));
        Assert.Equal("active", (string?)detail["client"]!["status"]);
        var invoice = detail["invoices"]!.AsArray().Single()!;
        Assert.Equal(300m, (decimal)invoice["total"]!);

        // Changing the plan later bills nothing by itself.
        detail = await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{created.OrgId}/plan", new { planId = plan, cycle = "annual", isComplimentary = false }));
        Assert.Single(detail["invoices"]!.AsArray());

        settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["taxLabel"] = "GST";
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
        Assert.Equal("VAT", (string?)(await Ok(await founder.GetAsync($"{Admin}/invoices/{invoice["id"]}")))["taxLabel"]);
    }

    [Fact]
    public async Task A_cancelled_client_is_told_why_and_plan_requests_can_be_listed()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag());
        var (orgId, client, _) = await NewClientAsync(founder, plan);
        await Ok(await client.PostAsJsonAsync("/api/v1/billing/change-request", new { planId = plan, cycle = "annual" }));
        var asking = (await Ok(await founder.GetAsync($"{Admin}/clients?status=planRequest&pageSize=200"))).AsArray();
        Assert.Contains(asking, c => (string?)c!["orgId"] == orgId.ToString());
        Assert.All(asking, c => Assert.True((bool)c!["hasPlanRequest"]!));

        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/cancel", new { reason = "Closed down" }));
        Assert.Equal("Closed down", (string?)(await Ok(await client.GetAsync("/api/v1/me")))["subscription"]!["suspendedReason"]);
        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/reactivate", null));
        var log = (await Ok(await founder.GetAsync($"{Admin}/activity?orgId={orgId}"))).AsArray();
        Assert.Contains(log, a => (string?)a!["action"] == "Reactivated" && ((string?)a!["detail"])!.Contains("on trial"));
    }

    [Fact]
    public async Task The_platforms_own_workspace_is_not_listed_as_a_client()
    {
        var (founder, _) = await FounderAsync();
        var clients = (await Ok(await founder.GetAsync($"{Admin}/clients?pageSize=200"))).AsArray();
        Assert.DoesNotContain(clients, c => (string?)c!["code"] == "platform-hq");
        Assert.Contains(clients, c => (string?)c!["code"] == "demo" && c!["status"] is null);
    }

    [Fact]
    public async Task Plan_limits_hold_for_users_legal_entities_and_modules()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag(), maxUsers: 1, maxEntities: 1, modules: ["crm"]);
        var (orgId, client, _) = await NewClientAsync(founder, plan);

        var invite = await client.PostAsJsonAsync("/api/v1/users", new { email = $"extra+{Tag()}@example.test", fullName = "Extra", temporaryPassword = ClientPassword, roleIds = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Conflict, invite.StatusCode);
        Assert.Contains("active users", await invite.Content.ReadAsStringAsync());

        var entity = await client.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Second", countryCode = "AE", baseCurrency = "AED" });
        Assert.Equal(HttpStatusCode.Conflict, entity.StatusCode);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/v1/modules/inventory/enable", null)).StatusCode);
        await Ok(await client.PostAsync("/api/v1/modules/crm/enable", null));

        // The founder can only switch on what the plan includes, too.
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsync($"{Admin}/clients/{orgId}/modules/inventory/enable", null)).StatusCode);

        // A plan with room lifts the limits.
        var bigger = await NewPlanAsync(founder, Tag());
        await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgId}/plan", new { planId = bigger, cycle = "monthly", isComplimentary = false }));
        await Ok(await client.PostAsJsonAsync("/api/v1/users", new { email = $"extra+{Tag()}@example.test", fullName = "Extra", temporaryPassword = ClientPassword, roleIds = Array.Empty<Guid>() }), HttpStatusCode.Created);
        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/modules/inventory/enable", null));

        // Back down to the small plan: what it does not include is switched off, and its data stays.
        await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgId}/plan", new { planId = plan, cycle = "monthly", isComplimentary = false }));
        var modules = (await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}")))["enabledModules"]!.AsArray().Select(m => (string?)m).ToList();
        Assert.Contains("crm", modules);
        Assert.DoesNotContain("inventory", modules);
    }

    [Fact]
    public async Task The_gate_pauses_changes_when_a_trial_ends_and_blocks_when_suspended()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag(), trialDays: 5);
        var (orgId, client, _) = await NewClientAsync(founder, plan);
        await Ok(await client.GetAsync("/api/v1/legal-entities"));

        var result = await RunBillingAsync(Today.AddDays(6));
        Assert.True(result.TrialsExpired >= 1);
        // The date has not really moved, so the founder sees the new state while the gate reads it from the row.
        var me = await Ok(await client.GetAsync("/api/v1/me"));
        Assert.Equal("expired", (string?)me["subscription"]!["status"]);
        Assert.Equal("readOnly", (string?)me["subscription"]!["access"]);

        await Ok(await client.GetAsync("/api/v1/legal-entities")); // viewing still works
        var write = await client.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Blocked", countryCode = "AE", baseCurrency = "AED" });
        Assert.Equal(HttpStatusCode.PaymentRequired, write.StatusCode);
        Assert.Contains("subscription-read-only", await write.Content.ReadAsStringAsync());
        await Ok(await client.GetAsync("/api/v1/billing/subscription")); // and so does the way out

        // More trial days bring it back at once.
        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/extend-trial", new { days = 7 }));
        Assert.Equal("full", (string?)(await Ok(await client.GetAsync("/api/v1/me")))["subscription"]!["access"]);

        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/suspend", new { reason = "Asked to pause" }));
        var blocked = await client.GetAsync("/api/v1/legal-entities");
        Assert.Equal(HttpStatusCode.PaymentRequired, blocked.StatusCode);
        Assert.Contains("subscription-blocked", await blocked.Content.ReadAsStringAsync());
        await Ok(await client.GetAsync("/api/v1/me"));
        await Ok(await client.GetAsync("/api/v1/billing/subscription"));

        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/reactivate", null));
        await Ok(await client.GetAsync("/api/v1/legal-entities"));

        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/cancel", new { reason = "Closed down" }));
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.GetAsync("/api/v1/legal-entities")).StatusCode);
        await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/reactivate", null));
        await Ok(await client.GetAsync("/api/v1/legal-entities"));

        // A workspace with no subscription is not held back.
        var (demo, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await Ok(await demo.GetAsync("/api/v1/legal-entities"));
        Assert.False((bool)(await Ok(await demo.GetAsync("/api/v1/me")))["subscription"]!["hasSubscription"]!);
    }

    [Fact]
    public async Task Billing_runs_from_activation_to_renewal_overdue_payment_and_suspension()
    {
        var (founder, _) = await FounderAsync();
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["taxPercent"] = 5m;
        settings["currency"] = "AED";
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
        var plan = await NewPlanAsync(founder, Tag(), price: 200);
        var (orgId, client, _) = await NewClientAsync(founder, plan);

        // Trial to paid: first invoice, with tax.
        var detail = await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/activate", new { issueInvoice = true }));
        Assert.Equal("active", (string?)detail["client"]!["status"]);
        var first = detail["invoices"]!.AsArray().Single()!;
        Assert.Equal(210m, (decimal)first["total"]!);
        Assert.Equal(210m, (decimal)detail["client"]!["openBalance"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/activate", new { issueInvoice = true })).StatusCode);

        var periodEnd = DateOnly.Parse((string)detail["client"]!["currentPeriodEnd"]!);
        await Ok(await founder.PostAsJsonAsync($"{Admin}/invoices/{first["id"]}/payment", new { method = "Bank transfer", reference = "TT-1" }));

        // Near the end of the period the renewal goes out once, however often the run happens.
        await RunBillingAsync(periodEnd.AddDays(-3));
        await RunBillingAsync(periodEnd.AddDays(-2));
        detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}"));
        var invoices = detail["invoices"]!.AsArray();
        Assert.Equal(2, invoices.Count);
        var renewal = invoices.First(i => (string?)i!["status"] == "open")!;
        Assert.Equal(periodEnd.ToString("yyyy-MM-dd"), (string?)renewal["dueOn"]);

        // Unpaid after its due date: overdue, then suspended, and paying brings it back.
        await RunBillingAsync(periodEnd.AddDays(1));
        detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}"));
        Assert.Equal("pastDue", (string?)detail["client"]!["status"]);

        var suspendAfter = (int)settings["suspendAfterDays"]!;
        await RunBillingAsync(periodEnd.AddDays(1 + suspendAfter));
        detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}"));
        Assert.Equal("suspended", (string?)detail["client"]!["status"]);
        Assert.False((bool)detail["suspendedByFounder"]!);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await client.GetAsync("/api/v1/legal-entities")).StatusCode);

        var paid = await Ok(await founder.PostAsJsonAsync($"{Admin}/invoices/{renewal["id"]}/payment", new { method = "Card", reference = "C-2" }));
        Assert.Equal("paid", (string?)paid["status"]);
        detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}"));
        Assert.Equal("active", (string?)detail["client"]!["status"]);
        Assert.Equal(SubscriptionEnd(periodEnd), (string?)detail["client"]!["currentPeriodEnd"]);
        await Ok(await client.GetAsync("/api/v1/legal-entities"));

        // A payment entered by mistake can be taken back; a paid invoice cannot be voided or paid twice.
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsJsonAsync($"{Admin}/invoices/{renewal["id"]}/void", new { reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsJsonAsync($"{Admin}/invoices/{renewal["id"]}/payment", new { method = "Card" })).StatusCode);
        Assert.Equal("open", (string?)(await Ok(await founder.PostAsync($"{Admin}/invoices/{renewal["id"]}/reopen", null)))["status"]);
        Assert.Equal("void", (string?)(await Ok(await founder.PostAsJsonAsync($"{Admin}/invoices/{renewal["id"]}/void", new { reason = "Wrong amount" })))["status"]);

        var dashboard = await Ok(await founder.GetAsync($"{Admin}/dashboard"));
        Assert.Equal("AED", (string?)dashboard["currency"]);
        Assert.True((decimal)dashboard["monthlyRecurring"]! >= 200m);
        Assert.Equal((decimal)dashboard["monthlyRecurring"]! * 12, (decimal)dashboard["annualRecurring"]!);
        Assert.True((decimal)dashboard["collectedTotal"]! >= 210m);
    }

    private static string SubscriptionEnd(DateOnly previousEnd)
    {
        var start = previousEnd.AddDays(1);
        return start.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
    }

    [Fact]
    public async Task The_founders_own_suspension_is_not_lifted_by_a_payment()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag(), price: 100);
        var (orgId, _, _) = await NewClientAsync(founder, plan);
        var detail = await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/activate", new { issueInvoice = true }));
        var invoice = detail["invoices"]!.AsArray().Single()!;
        await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/suspend", new { reason = "Abuse" }));
        await Ok(await founder.PostAsJsonAsync($"{Admin}/invoices/{invoice["id"]}/payment", new { method = "Card" }));
        Assert.Equal("suspended", (string?)(await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}")))["client"]!["status"]);
    }

    [Fact]
    public async Task A_client_sees_only_its_own_billing_and_can_ask_for_a_plan_change()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag());
        var target = await NewPlanAsync(founder, Tag());
        var (orgA, a, _) = await NewClientAsync(founder, plan);
        var (orgB, b, _) = await NewClientAsync(founder, plan);
        var invoice = await Ok(await founder.PostAsJsonAsync($"{Admin}/clients/{orgA}/invoices", new { description = "Setup help", amount = 50m }));

        Assert.Single((await Ok(await a.GetAsync("/api/v1/billing/subscription")))["invoices"]!.AsArray());
        Assert.Empty((await Ok(await b.GetAsync("/api/v1/billing/subscription")))["invoices"]!.AsArray());
        await Ok(await a.GetAsync($"/api/v1/billing/invoices/{invoice["id"]}"));
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/v1/billing/invoices/{invoice["id"]}")).StatusCode);

        var billing = await Ok(await a.PostAsJsonAsync("/api/v1/billing/change-request", new { planId = target, cycle = "annual", note = "We are growing" }));
        Assert.Equal(target.ToString(), (string?)billing["requested"]!["planId"]);
        var detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgA}"));
        Assert.True((bool)detail["client"]!["hasPlanRequest"]!);
        Assert.Equal("We are growing", (string?)detail["requested"]!["note"]);
        Assert.Null((await Ok(await founder.GetAsync($"{Admin}/clients/{orgB}")))["requested"]);

        detail = await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgA}/plan", new { planId = target, cycle = "annual", isComplimentary = false }));
        Assert.Equal(target.ToString(), (string?)detail["client"]!["planId"]);
        Assert.Null(detail["requested"]);
    }

    [Fact]
    public async Task Free_of_charge_clients_never_lapse_or_get_invoiced()
    {
        var (founder, _) = await FounderAsync();
        var plan = await NewPlanAsync(founder, Tag(), trialDays: 3);
        var (orgId, client, _) = await NewClientAsync(founder, plan);
        await Ok(await founder.PutAsJsonAsync($"{Admin}/clients/{orgId}/plan", new { planId = plan, cycle = "monthly", isComplimentary = true }));
        await RunBillingAsync(Today.AddDays(400));
        var detail = await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}"));
        Assert.Equal("active", (string?)detail["client"]!["status"]);
        Assert.Empty(detail["invoices"]!.AsArray());
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsJsonAsync($"{Admin}/clients/{orgId}/invoices", new { })).StatusCode);
        await Ok(await client.GetAsync("/api/v1/legal-entities"));
    }

    [Fact]
    public async Task The_platform_team_can_be_changed_and_a_removed_member_loses_access_at_once()
    {
        var (founder, session) = await FounderAsync();
        var tag = Tag();
        var added = await Ok(await founder.PostAsJsonAsync($"{Admin}/team", new { email = $"manager+{tag}@example.test", fullName = "Manager" }));
        var password = (string)added["temporaryPassword"]!;
        var (manager, _) = await f.SignInAsync($"manager+{tag}@example.test", password);
        await Ok(await manager.GetAsync($"{Admin}/dashboard"));

        var id = (string)added["member"]!["userId"]!;
        await Ok(await founder.DeleteAsync($"{Admin}/team/{id}"), HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"{Admin}/dashboard")).StatusCode);

        var me = await Ok(await founder.GetAsync("/api/v1/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await founder.DeleteAsync($"{Admin}/team/{me["user"]!["id"]}")).StatusCode);
        Assert.NotNull(session);
    }

    [Fact]
    public async Task A_temporary_password_replaces_the_old_one_and_leaves_a_trace()
    {
        var (founder, _) = await FounderAsync();
        var (orgId, _, email) = await NewClientAsync(founder);
        var member = (await Ok(await founder.GetAsync($"{Admin}/clients/{orgId}")))["members"]!.AsArray().Single()!;
        var temp = (string)(await Ok(await founder.PostAsync($"{Admin}/clients/{orgId}/users/{member["userId"]}/temporary-password", null)))["password"]!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email, password = ClientPassword })).StatusCode);
        var (client, _) = await f.SignInAsync(email, temp);
        Assert.True((bool)(await Ok(await client.GetAsync("/api/v1/me")))["user"]!["mustChangePassword"]!);
        var log = (await Ok(await founder.GetAsync($"{Admin}/activity?orgId={orgId}"))).AsArray();
        Assert.Contains(log, a => (string?)a!["action"] == "Temporary password issued" && ((string?)a!["detail"])!.Contains(email));

        // A random member of another workspace is out of reach from this client's page.
        var (other, _, _) = await NewClientAsync(founder);
        Assert.Equal(HttpStatusCode.NotFound, (await founder.PostAsync($"{Admin}/clients/{other}/users/{member["userId"]}/temporary-password", null)).StatusCode);
    }

    [Fact]
    public async Task Plans_cannot_clash_and_the_default_plan_cannot_be_archived()
    {
        var (founder, _) = await FounderAsync();
        var tag = Tag();
        var id = await NewPlanAsync(founder, tag);
        var clash = await founder.PostAsJsonAsync($"{Admin}/plans", new { code = "p-" + tag, name = "Clash", isPublic = true, isRecommended = false, sortOrder = 1, monthlyPrice = 1m, annualPrice = 1m, trialDays = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        var negative = await founder.PostAsJsonAsync($"{Admin}/plans", new { code = "neg-" + tag, name = "Neg", isPublic = true, isRecommended = false, sortOrder = 1, monthlyPrice = -1m, annualPrice = 1m, trialDays = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["defaultPlanId"] = id.ToString();
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PostAsync($"{Admin}/plans/{id}/archive", null)).StatusCode);

        var config = await Ok(await f.Client().GetAsync("/api/v1/public/signup-config"));
        Assert.Equal(id.ToString(), (string?)config["defaultPlanId"]);
        Assert.Contains(config["plans"]!.AsArray(), p => (string?)p!["id"] == id.ToString());
    }

    [Fact]
    public async Task The_older_onboarding_route_starts_on_the_default_plan()
    {
        var tag = Tag();
        var res = await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", new
        {
            workspaceName = "Old " + tag, signInKey = "o-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", timeZone = "Asia/Dubai",
            adminEmail = $"old+{tag}@example.test", adminName = "Old", adminPassword = ClientPassword,
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var (client, _) = await f.SignInAsync($"old+{tag}@example.test", ClientPassword);
        Assert.Equal("trialing", (string?)(await Ok(await client.GetAsync("/api/v1/me")))["subscription"]!["status"]);
    }
}

/// <summary>Browser tabs share one refresh cookie, so a tab can send a token another tab has only just replaced.</summary>
public class BrowserTabsTests(BrowserTabsTests.TabsFactory f) : IClassFixture<BrowserTabsTests.TabsFactory>
{
    public sealed class TabsFactory : ApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Auth:RefreshReuseLeewaySeconds", "2");
        }
    }

    private async Task<string> SignInAsync()
    {
        var res = await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password });
        return CookieOf(res);
    }

    private static string CookieOf(HttpResponseMessage res) => res.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("erp_rt=")).Split(';')[0];

    private async Task<HttpResponseMessage> RefreshAsync(string cookie)
    {
        var client = f.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-ERP-Client", "tests");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", cookie);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task A_second_tab_with_a_just_replaced_cookie_keeps_everyone_signed_in()
    {
        var first = await SignInAsync();
        var tabOne = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, tabOne.StatusCode);
        var tabTwo = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, tabTwo.StatusCode);
        Assert.NotNull(JsonNode.Parse(await tabTwo.Content.ReadAsStringAsync())!["data"]!["accessToken"]);

        // Both tabs carry on, whichever cookie the browser kept.
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(CookieOf(tabOne))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(CookieOf(tabTwo))).StatusCode);
    }

    [Fact]
    public async Task An_old_token_used_after_the_window_still_ends_the_whole_sign_in()
    {
        var first = await SignInAsync();
        var replaced = await RefreshAsync(first);
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(first)).StatusCode); // inside the window: does not reopen it

        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(CookieOf(replaced))).StatusCode);
    }
}

/// <summary>What the public pages can read.</summary>
public class PublicPagesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Admin = "/api/v1/platform-admin";

    private static async Task<JsonNode> Ok(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"Got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Terms_and_privacy_are_public_filled_in_and_editable_by_the_founder()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        Assert.True((bool)settings["legalIsStarterText"]!);
        Assert.Contains("{company}", (string)settings["termsText"]!); // the founder edits the wording with its placeholders

        settings["companyName"] = "Acme Software LLC";
        settings["supportEmail"] = "help@acme.test";
        var saved = await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
        Assert.True((bool)saved["legalIsStarterText"]!); // sending the starter wording back keeps it the starter wording

        var legal = await Ok(await f.Client().GetAsync("/api/v1/public/legal"));
        Assert.Contains("Acme Software LLC", (string)legal["terms"]!);
        Assert.Contains("help@acme.test", (string)legal["privacy"]!);
        Assert.DoesNotContain("{company}", (string)legal["terms"]!);
        Assert.DoesNotContain("{email}", (string)legal["privacy"]!);
        Assert.NotNull(legal["updatedAt"]);

        saved["termsText"] = "Our own terms for {company}.";
        saved = await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", saved));
        Assert.False((bool)saved["legalIsStarterText"]!);
        legal = await Ok(await f.Client().GetAsync("/api/v1/public/legal"));
        Assert.Equal("Our own terms for Acme Software LLC.", (string)legal["terms"]!);
        Assert.NotNull(legal["updatedAt"]);

        // Emptying the box goes back to the starter wording, and a link must be a real web link.
        saved["termsText"] = "";
        saved = await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", saved));
        Assert.True((bool)saved["legalIsStarterText"]!);
        saved["termsUrl"] = "javascript:alert(1)";
        Assert.Equal(HttpStatusCode.BadRequest, (await founder.PutAsJsonAsync($"{Admin}/settings", saved)).StatusCode);
    }

    [Fact]
    public async Task The_module_list_is_public_and_says_nothing_about_any_workspace()
    {
        var modules = (await Ok(await f.Client().GetAsync("/api/v1/public/modules"))).AsArray();
        Assert.NotEmpty(modules);
        Assert.All(modules, m => Assert.Equal(new[] { "category", "description", "isCore", "key", "name", "requires" }, m!.AsObject().Select(p => p.Key).OrderBy(k => k)));
    }
}

/// <summary>The email route of registration, with an email server standing in.</summary>
public class SignupVerificationTests(SignupVerificationTests.EmailFactory f) : IClassFixture<SignupVerificationTests.EmailFactory>
{
    public sealed class CapturingMailer : IPlatformMailer
    {
        public System.Collections.Concurrent.ConcurrentQueue<(string To, string Subject, string Body)> Sent { get; } = new();
        public bool IsEnabled => true;
        public string Link(string path) => "https://app.test" + path;
        public Task SendAsync(string? to, string subject, string body, CancellationToken ct = default)
        {
            if (to is not null) Sent.Enqueue((to, subject, body));
            return Task.CompletedTask;
        }
    }

    public sealed class EmailFactory : ApiFactory
    {
        public CapturingMailer Mailer { get; } = new();
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<IPlatformMailer>();
                s.AddSingleton<IPlatformMailer>(Mailer);
            });
        }
    }

    private const string Admin = "/api/v1/platform-admin";

    private static async Task<JsonNode> Ok(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"Got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private string SignupAndGetToken(string email)
    {
        var mail = f.Mailer.Sent.Last(m => m.To == email && m.Subject == "Confirm your email");
        var match = System.Text.RegularExpressions.Regex.Match(mail.Body, @"token=([^\s]+)");
        Assert.True(match.Success);
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }

    private async Task SetModeAsync(string mode)
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var settings = (await Ok(await founder.GetAsync($"{Admin}/settings"))).AsObject();
        settings["signupMode"] = mode;
        settings["requireEmailVerification"] = true;
        await Ok(await founder.PutAsJsonAsync($"{Admin}/settings", settings));
    }

    private static object Body(string tag) => new
    {
        businessName = "Verified " + tag, signInKey = "v-" + tag, industry = "generic", countryCode = "AE", baseCurrency = "AED", contactName = "Owner", email = $"v+{tag}@example.test",
        password = "Qa#Passw0rd1", cycle = "monthly", acceptTerms = true,
    };

    [Fact]
    public async Task Open_mode_creates_the_workspace_when_the_email_is_confirmed_and_the_link_works_once()
    {
        await SetModeAsync("open");
        var tag = Guid.NewGuid().ToString("N")[..8];
        var result = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Body(tag)));
        Assert.Equal("pendingEmail", (string?)result["status"]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = $"v+{tag}@example.test", password = "Qa#Passw0rd1" })).StatusCode);

        var token = SignupAndGetToken($"v+{tag}@example.test");
        var verified = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token }));
        Assert.Equal("provisioned", (string?)verified["status"]);
        await f.SignInAsync($"v+{tag}@example.test", "Qa#Passw0rd1");
        Assert.Contains(f.Mailer.Sent, m => m.To == $"v+{tag}@example.test" && m.Subject == "Your workspace is ready");
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token = "made-up" })).StatusCode);
    }

    [Fact]
    public async Task Review_mode_confirms_the_email_first_then_waits_for_the_founder()
    {
        await SetModeAsync("review");
        var tag = Guid.NewGuid().ToString("N")[..8];
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Body(tag)));
        var verified = await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token = SignupAndGetToken($"v+{tag}@example.test") }));
        Assert.Equal("pendingReview", (string?)verified["status"]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = $"v+{tag}@example.test", password = "Qa#Passw0rd1" })).StatusCode);
    }

    [Fact]
    public async Task Registering_again_replaces_the_earlier_link()
    {
        await SetModeAsync("open");
        var tag = Guid.NewGuid().ToString("N")[..8];
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Body(tag)));
        var first = SignupAndGetToken($"v+{tag}@example.test");
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups", Body(tag)));
        var second = SignupAndGetToken($"v+{tag}@example.test");
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token = first })).StatusCode);
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/public/signups/verify", new { token = second }));
    }
}
