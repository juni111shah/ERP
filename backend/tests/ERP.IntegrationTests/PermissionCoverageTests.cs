using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ERP.Api.Infrastructure;
using ERP.Application.Platform;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modularity.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>
/// Sweeps every API endpoint for its permission. Each one must say who may call it: a <c>[RequirePermission]</c>, a named policy (the
/// founder's console, the portals), <c>[AllowAnonymous]</c>, <c>[SignedIn("why")]</c> for the caller's own things, or
/// <c>[PermissionByRequest(typeof(...))]</c> when the request decides the permission. A bare <c>[Authorize]</c> fails, so a new endpoint
/// cannot slip through with "any signed-in user" by accident. Then a member holding no permission at all is refused (403) by every
/// permission-protected endpoint, with every module switched on so the module gate cannot answer first. The founder's console has its own
/// rights on top (support, manager, owner): every change there names the right it needs, and a support member is refused the rest.
/// </summary>
public partial class PermissionCoverageTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly string[] NamedPolicies = ["platform-admin", PlatformRights.ManagerPolicy, PlatformRights.OwnerPolicy, "portal", "portal.customer", "portal.vendor"];

    private List<RouteEndpoint> ApiEndpoints()
    {
        _ = f.CreateClient();
        var api = f.Services.GetServices<EndpointDataSource>().SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText is { } t && t.TrimStart('/').StartsWith("api/", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(api.Count > 500, $"Only {api.Count} api endpoints found");
        return api;
    }

    private static string Name(RouteEndpoint e) => $"{string.Join(",", e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"])} {e.RoutePattern.RawText}";

    private static IEnumerable<string> RequiredPermissions(RouteEndpoint e) => e.Metadata.GetOrderedMetadata<IAuthorizeData>()
        .Select(a => a.Policy).OfType<string>().Where(p => p.StartsWith(PermissionPolicies.PolicyPrefix, StringComparison.Ordinal))
        .Select(p => p[PermissionPolicies.PolicyPrefix.Length..]);

    [Fact]
    public void Every_api_endpoint_declares_a_permission_or_says_why_it_needs_none()
    {
        var problems = new List<string>();
        foreach (var e in ApiEndpoints())
        {
            var auth = e.Metadata.GetOrderedMetadata<IAuthorizeData>().ToList();
            var anonymous = e.Metadata.GetMetadata<IAllowAnonymous>() is not null;
            var permission = RequiredPermissions(e).Any();
            var named = auth.Any(a => a.Policy is { } p && NamedPolicies.Contains(p));
            var signedIn = e.Metadata.GetMetadata<SignedInAttribute>();
            var byRequest = e.Metadata.GetMetadata<PermissionByRequestAttribute>();

            if (auth.Any(a => a.GetType() == typeof(AuthorizeAttribute) && a.Policy is null && a.Roles is null))
                problems.Add($"{Name(e)}: bare [Authorize]. Use [RequirePermission], [PermissionByRequest(typeof(...))] or [SignedIn(\"why\")].");
            else if (!(anonymous || permission || named || signedIn is not null || byRequest is not null))
                problems.Add($"{Name(e)}: says nothing about who may call it.");
            if (signedIn is not null && string.IsNullOrWhiteSpace(signedIn.Reason))
                problems.Add($"{Name(e)}: [SignedIn] needs a reason.");
            if (byRequest is not null)
            {
                if (byRequest.Requests.Count == 0) problems.Add($"{Name(e)}: [PermissionByRequest] names no request.");
                foreach (var t in byRequest.Requests.Where(t => !typeof(IRequirePermission).IsAssignableFrom(t) && !typeof(IAuthorizesInHandler).IsAssignableFrom(t)))
                    problems.Add($"{Name(e)}: {t.Name} must implement IRequirePermission or IAuthorizesInHandler.");
            }
        }
        Assert.True(problems.Count == 0, "Endpoints without a clear permission:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_permission_an_endpoint_asks_for_is_in_the_catalogue()
    {
        var catalog = f.Services.GetRequiredService<IPermissionCatalog>().All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var unknown = ApiEndpoints().SelectMany(e => RequiredPermissions(e).Where(p => !catalog.Contains(p)).Select(p => $"{Name(e)}: {p}")).Distinct().ToList();
        Assert.True(unknown.Count == 0, "Endpoints asking for a permission no role can grant:\n" + string.Join("\n", unknown));
    }

    [Fact]
    public void Every_menu_item_is_guarded_by_a_permission_in_the_catalogue()
    {
        var catalog = f.Services.GetRequiredService<IPermissionCatalog>().All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        var items = CoreMenu.Items.Concat(f.Services.GetRequiredService<ModuleRegistry>().Modules.SelectMany(m => m.Menu)).ToList();
        // Open to every member on purpose: the home page, the approvals inbox (each request is filtered) and their own HR page.
        var open = new HashSet<string>(["core.overview", "core.approvals", "hr.my"], StringComparer.Ordinal);
        var problems = items.Where(i => i.Permission is null ? !open.Contains(i.Id) : !catalog.Contains(i.Permission))
            .Select(i => $"{i.Id} ({i.Route}): {i.Permission ?? "no permission"}").ToList();
        Assert.True(problems.Count == 0, "Menu items without a known permission:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_permission_is_named_module_resource_action_in_lower_case_and_described()
    {
        var bad = f.Services.GetRequiredService<IPermissionCatalog>().All
            .Where(p => !KeyPattern().IsMatch(p.Key) || p.Action == "view" || string.IsNullOrWhiteSpace(p.Description))
            .Select(p => $"{p.Key} ({p.Description})").ToList();
        Assert.True(bad.Count == 0, "Permissions breaking the naming rule (<module>.<resource>.<action>, lower case, .read not .view, with a description):\n" + string.Join("\n", bad));
    }

    [Fact]
    public async Task A_member_holding_no_permission_is_refused_by_every_permission_protected_endpoint()
    {
        // 1. Every module on, so a 403 comes from the permission and not from the module gate.
        var (owner, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await EnableEveryModuleAsync(owner);
        var (admin, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");

        // 2. A member whose only role grants nothing.
        var nobody = await MemberWithAsync(admin, []);

        // 3. Every permission-protected route, called with placeholder route values and an empty body.
        var failures = new List<string>();
        var gated = new List<string>();
        var checkedCount = 0;
        foreach (var e in ApiEndpoints().Where(e => RequiredPermissions(e).Any()))
        {
            var url = "/" + Fill(e.RoutePattern);
            foreach (var method in e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
            {
                using var req = new HttpRequestMessage(new HttpMethod(method), url);
                if (method is "POST" or "PUT" or "PATCH") req.Content = JsonContent.Create(new JsonObject());
                using var res = await nobody.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();
                checkedCount++;
                if (body.Contains("module-disabled", StringComparison.Ordinal)) gated.Add($"{method} {url}");
                else if (res.StatusCode != HttpStatusCode.Forbidden) failures.Add($"{method} {e.RoutePattern.RawText} -> {(int)res.StatusCode}: {Trim(body)}");
            }
        }

        Assert.True(failures.Count == 0, $"A member with no permission got past these {failures.Count} endpoints:\n" + string.Join("\n", failures));
        Assert.True(gated.Count == 0, "These answered module-disabled, so their permission was not exercised:\n" + string.Join("\n", gated));
        Assert.True(checkedCount > 500, $"Only {checkedCount} calls were checked");
    }

    [Fact]
    public async Task Taking_a_permission_away_ends_the_old_token_and_the_refreshed_session_is_refused()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var roleName = "Coverage " + Guid.NewGuid().ToString("N")[..6];
        var role = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new { name = roleName, description = "test", permissions = new[] { CorePermissions.PartiesRead, CorePermissions.ItemsRead } }), HttpStatusCode.Created))["id"]!;
        var member = await MemberWithRoleAsync(admin, role);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/parties")).StatusCode);

        // A grant only: the token keeps working.
        await HrKit.Ok(await admin.PutAsJsonAsync($"/api/v1/roles/{role}", new { name = roleName, description = "test", permissions = new[] { CorePermissions.PartiesRead, CorePermissions.ItemsRead, CorePermissions.TaxRead } }));
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/parties")).StatusCode);

        // Taken away: the old token stops at once...
        await HrKit.Ok(await admin.PutAsJsonAsync($"/api/v1/roles/{role}", new { name = roleName, description = "test", permissions = new[] { CorePermissions.ItemsRead } }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/items")).StatusCode);

        // ...and the silently refreshed session (same refresh cookie, no sign-in) carries the new set.
        var refreshed = await member.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        member.DefaultRequestHeaders.Authorization = new("Bearer", (string)(await ApiFactory.Json(refreshed))["accessToken"]!);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/items")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/v1/parties")).StatusCode);
        var me = await ApiFactory.Json(await member.GetAsync("/api/v1/me"));
        Assert.DoesNotContain(CorePermissions.PartiesRead, me["permissions"]!.AsArray().Select(p => (string)p!));
    }

    [Fact]
    public async Task Removing_a_role_from_a_member_ends_their_old_token()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var role = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new { name = "Coverage " + Guid.NewGuid().ToString("N")[..6], description = "test", permissions = new[] { CorePermissions.PartiesRead } }), HttpStatusCode.Created))["id"]!;
        var (member, userId) = await MemberWithRoleAndIdAsync(admin, role);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync("/api/v1/parties")).StatusCode);

        await HrKit.Ok(await admin.PutAsJsonAsync($"/api/v1/users/{userId}/roles", new { roleIds = Array.Empty<string>() }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/parties")).StatusCode);
    }

    // ---------- the founder's console ----------

    private const string Console = "api/v1/platform-admin";
    private static readonly string[] PlatformRightPolicies = [PlatformRights.ManagerPolicy, PlatformRights.OwnerPolicy];

    private IEnumerable<RouteEndpoint> ConsoleEndpoints() => ApiEndpoints().Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith(Console + "/", StringComparison.OrdinalIgnoreCase) || e.RoutePattern.RawText!.TrimStart('/') == Console);

    private static IReadOnlyList<string> Methods(RouteEndpoint e) => e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];

    private static string? PlatformRight(RouteEndpoint e) => e.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).FirstOrDefault(p => p is not null && PlatformRightPolicies.Contains(p));

    private static string ConsoleRoute(RouteEndpoint e) => e.RoutePattern.RawText!.TrimStart('/')[Console.Length..].TrimStart('/');

    [Fact]
    public void Every_founder_console_change_says_who_on_the_platform_team_may_make_it()
    {
        var problems = new List<string>();
        foreach (var e in ConsoleEndpoints())
        {
            if (!e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == "platform-admin")) problems.Add($"{Name(e)}: not behind the platform-admin policy.");
            if (Methods(e).All(m => m is "GET" or "HEAD")) continue;
            var team = e.Metadata.GetMetadata<PlatformTeamActionAttribute>();
            if (PlatformRight(e) is null && team is null)
                problems.Add($"{Name(e)}: a console change needs [PlatformManager], [PlatformOwner] or [PlatformTeamAction(\"why\")].");
            if (team is not null && string.IsNullOrWhiteSpace(team.Reason)) problems.Add($"{Name(e)}: [PlatformTeamAction] needs a reason.");
            if (team is not null && PlatformRight(e) is not null) problems.Add($"{Name(e)}: both a stricter right and [PlatformTeamAction]; pick one.");
        }
        Assert.True(problems.Count == 0, "Founder console endpoints without a clear right:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void The_founder_actions_that_hurt_when_they_go_wrong_need_a_manager()
    {
        // Suspend, cancel, reactivate, activate; plans and modules; voids and reopens; password resets; the team; settings and the billing
        // run; the price list; sign-up decisions; the demo flag. The AI assistant and who is a manager are for the owner alone.
        string[] manager =
        [
            "POST clients/{orgId:guid}/suspend", "POST clients/{orgId:guid}/cancel", "POST clients/{orgId:guid}/reactivate", "POST clients/{orgId:guid}/activate",
            "PUT clients/{orgId:guid}/plan", "PUT clients/{orgId:guid}/demo", "POST clients/{orgId:guid}/users/{userId:guid}/temporary-password",
            "POST clients/{orgId:guid}/modules/{key}/{action:regex(^(enable|disable)$)}",
            "POST invoices/{id:guid}/void", "POST invoices/{id:guid}/reopen",
            "POST team", "DELETE team/{userId:guid}",
            "PUT settings", "POST billing-run",
            "POST plans", "PUT plans/{id:guid}", "POST plans/{id:guid}/{action:regex(^(archive|restore)$)}",
            "POST signups/{id:guid}/approve", "POST signups/{id:guid}/reject",
        ];
        string[] owner =
        [
            "PUT team/{userId:guid}/role", "PUT assistant/visibility", "PUT assistant/limits", "POST assistant/providers", "PUT assistant/providers/order",
            "PUT assistant/providers/{id:guid}", "DELETE assistant/providers/{id:guid}", "POST assistant/providers/{id:guid}/test", "POST assistant/try",
        ];
        var byRoute = ConsoleEndpoints().SelectMany(e => Methods(e).Select(m => (Key: $"{m} {ConsoleRoute(e)}", Right: PlatformRight(e))))
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Select(x => x.Right).ToList());
        var problems = new List<string>();
        foreach (var (routes, wanted) in new[] { (manager, PlatformRights.ManagerPolicy), (owner, PlatformRights.OwnerPolicy) })
            foreach (var r in routes)
            {
                if (!byRoute.TryGetValue(r, out var rights)) problems.Add($"{r}: no such console endpoint (renamed? update this list)");
                else if (rights.Any(x => x is null || (wanted == PlatformRights.OwnerPolicy && x != PlatformRights.OwnerPolicy)))
                    problems.Add($"{r}: needs {wanted}, has {string.Join(", ", rights.Select(x => x ?? "the whole team"))}");
            }
        Assert.True(problems.Count == 0, "Founder actions without the stricter right:\n" + string.Join("\n", problems));
    }

    [Fact]
    public async Task A_support_member_of_the_platform_team_reads_everything_but_is_refused_every_manager_and_owner_action()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var support = await TeamMemberAsync(founder, "support");
        Assert.Equal("support", (string?)(await ApiFactory.Json(await support.GetAsync("/api/v1/me")))["platformRole"]);

        var failures = new List<string>();
        var refused = 0;
        foreach (var e in ConsoleEndpoints())
        {
            var url = "/" + Fill(e.RoutePattern);
            var right = PlatformRight(e);
            foreach (var method in Methods(e))
            {
                // Reads are open to the whole team. Of the changes, only those needing a stricter right are sent: they never reach a handler.
                if (method != "GET" && right is null) continue;
                using var req = new HttpRequestMessage(new HttpMethod(method), url);
                if (method is "POST" or "PUT" or "PATCH") req.Content = JsonContent.Create(new JsonObject());
                using var res = await support.SendAsync(req);
                if (method == "GET")
                {
                    if (res.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized) failures.Add($"GET {e.RoutePattern.RawText}: a support member could not read it ({(int)res.StatusCode})");
                    continue;
                }
                refused++;
                if (res.StatusCode != HttpStatusCode.Forbidden) failures.Add($"{method} {e.RoutePattern.RawText} -> {(int)res.StatusCode}: {Trim(await res.Content.ReadAsStringAsync())}");
            }
        }
        Assert.True(failures.Count == 0, "The platform team's rights do not hold:\n" + string.Join("\n", failures));
        Assert.True(refused >= 25, $"Only {refused} manager or owner actions were checked");
    }

    [Fact]
    public async Task A_manager_runs_the_risky_actions_but_not_the_owners_and_only_the_owner_makes_managers()
    {
        var (founder, _) = await f.SignInAsync(ApiFactory.FounderEmail);
        var (manager, managerId) = await TeamMemberWithIdAsync(founder, "manager");
        Assert.Equal("manager", (string?)(await ApiFactory.Json(await manager.GetAsync("/api/v1/me")))["platformRole"]);
        Assert.Equal("owner", (string?)(await ApiFactory.Json(await founder.GetAsync("/api/v1/me")))["platformRole"]);

        // A manager's right works; the owner's do not.
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsync($"/{Console}/billing-run", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/{Console}/assistant/visibility", new { brandEnabled = true, workspaceEnabled = false })).StatusCode);
        var (_, supportId) = await TeamMemberWithIdAsync(founder, "support");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/{Console}/team/{supportId}/role", new { role = "manager" })).StatusCode);

        // A manager adds support members, not managers, and cannot take another manager off the team.
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/{Console}/team", new { email = $"m2_{Guid.NewGuid():N}@platform.test", fullName = "Second", role = "manager" })).StatusCode);
        var (_, otherManager) = await TeamMemberWithIdAsync(founder, "manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/{Console}/team/{otherManager}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await manager.DeleteAsync($"/{Console}/team/{supportId}")).StatusCode);

        // The owner makes the manager support again: the next risky call is refused, with no new sign-in.
        var changed = await ApiFactory.Json(await founder.PutAsJsonAsync($"/{Console}/team/{managerId}/role", new { role = "support" }));
        Assert.Equal("support", (string?)changed["role"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsync($"/{Console}/billing-run", null)).StatusCode);

        // The owner's own role is fixed.
        var me = await ApiFactory.Json(await founder.GetAsync("/api/v1/me"));
        Assert.Equal(HttpStatusCode.Conflict, (await founder.PutAsJsonAsync($"/{Console}/team/{me["user"]!["id"]}/role", new { role = "support" })).StatusCode);
    }

    private async Task<HttpClient> TeamMemberAsync(HttpClient founder, string role) => (await TeamMemberWithIdAsync(founder, role)).Client;

    private async Task<(HttpClient Client, string UserId)> TeamMemberWithIdAsync(HttpClient founder, string role)
    {
        var email = $"team_{Guid.NewGuid():N}@platform.test";
        var res = await founder.PostAsJsonAsync($"/{Console}/team", new { email, fullName = "Team " + role, role });
        Assert.True(res.IsSuccessStatusCode, $"Adding a {role} failed: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        var added = await ApiFactory.Json(res);
        var (client, _) = await f.SignInAsync(email, (string)added["temporaryPassword"]!);
        return (client, (string)added["member"]!["userId"]!);
    }

    // ---------- helpers ----------

    private static async Task EnableEveryModuleAsync(HttpClient owner)
    {
        // Several passes: a module that requires another is refused until that one is on.
        for (var pass = 0; pass < 4; pass++)
        {
            var modules = (await ApiFactory.Json(await owner.GetAsync("/api/v1/modules"))).AsArray();
            var off = modules.Where(m => m!["isCore"]?.GetValue<bool>() != true && (string?)m!["state"] != "enabled").Select(m => (string)m!["key"]!).ToList();
            if (off.Count == 0) return;
            foreach (var key in off) await owner.PostAsync($"/api/v1/modules/{key}/enable", null);
        }
    }

    private async Task<HttpClient> MemberWithAsync(HttpClient admin, string[] permissions)
    {
        var role = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/roles", new { name = "Nothing " + Guid.NewGuid().ToString("N")[..6], description = "test", permissions }), HttpStatusCode.Created))["id"]!;
        return await MemberWithRoleAsync(admin, role);
    }

    private async Task<HttpClient> MemberWithRoleAsync(HttpClient admin, string roleId) => (await MemberWithRoleAndIdAsync(admin, roleId)).Client;

    private async Task<(HttpClient Client, string UserId)> MemberWithRoleAndIdAsync(HttpClient admin, string roleId)
    {
        var email = $"coverage_{Guid.NewGuid():N}@demo.local";
        var user = await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Coverage User", temporaryPassword = HrKit.Password, roleIds = new[] { roleId } }), HttpStatusCode.Created);
        var (client, _) = await f.SignInAsync(email, HrKit.Password, "demo-re");
        return (client, (string)user["id"]!);
    }

    /// <summary>The route with a value that satisfies each parameter's constraint (a new GUID, the first regex choice, 1, "x").</summary>
    private static string Fill(RoutePattern pattern) => string.Join("/", pattern.PathSegments.Select(s => string.Concat(s.Parts.Select(p => p switch
    {
        RoutePatternLiteralPart l => l.Content,
        RoutePatternSeparatorPart sep => sep.Content,
        RoutePatternParameterPart param => Value(param),
        _ => "",
    })))).Replace("//", "/").TrimEnd('/');

    private static string Value(RoutePatternParameterPart p)
    {
        foreach (var c in p.ParameterPolicies.Select(x => x.Content ?? ""))
        {
            if (c == "guid") return Guid.NewGuid().ToString();
            if (c is "int" or "long" or "decimal" or "double") return "1";
            if (c is "datetime") return "2030-01-01";
            if (c == "bool") return "true";
            var regex = RegexChoice().Match(c);
            if (regex.Success) return regex.Groups[1].Value.Split('|')[0];
        }
        return p.IsOptional ? "" : "x";
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "..." : s;

    // The action may be hyphenated (pos.orders.override-price); roles already store those keys.
    [GeneratedRegex(@"^[a-z][a-z0-9]*\.[a-z][a-z0-9]*\.[a-z][a-z0-9-]*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^regex\(\^\((.+)\)\$\)$")]
    private static partial Regex RegexChoice();
}
