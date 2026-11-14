using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
/// permission-protected endpoint, with every module switched on so the module gate cannot answer first.
/// </summary>
public partial class PermissionCoverageTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly string[] NamedPolicies = ["platform-admin", "portal", "portal.customer", "portal.vendor"];

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

    [GeneratedRegex(@"^[a-z][a-z0-9]*\.[a-z][a-z0-9]*\.[a-z][a-z0-9]*$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^regex\(\^\((.+)\)\$\)$")]
    private static partial Regex RegexChoice();
}
