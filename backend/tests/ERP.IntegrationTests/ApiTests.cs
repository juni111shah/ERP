using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

public class AuthTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_returns_short_lived_token_and_hardened_session_cookie()
    {
        var c = f.Client();
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await ApiFactory.Json(res);
        Assert.Equal(300, (int)body["expiresIn"]!);
        Assert.Null(body["refreshToken"]);
        var cookie = res.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("erp_rt="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase); // session-only without "keep me signed in"

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken((string)body["accessToken"]!);
        Assert.Contains(jwt.Claims, x => x.Type == "org");
        Assert.Contains(jwt.Claims, x => x.Type == "perm" && x.Value == "core.users.write");
        Assert.True((jwt.ValidTo - jwt.ValidFrom).TotalMinutes <= 5.1);
    }

    [Fact]
    public async Task Keep_me_signed_in_gives_a_30_day_cookie()
    {
        var res = await f.Client().PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password, rememberMe = true });
        var cookie = res.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("erp_rt="));
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_generic_401()
    {
        var c = f.Client();
        var a = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = "Wrong#Passw0rd" });
        var b = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@demo.local", password = "Wrong#Passw0rd" });
        Assert.Equal(HttpStatusCode.Unauthorized, a.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, b.StatusCode);
        Assert.Equal((string?)(await ApiFactory.Json(a))["detail"], (string?)(await ApiFactory.Json(b))["detail"]);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_with_a_retry_time()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var viewerRole = (await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"))).AsArray().First(r => (string?)r!["name"] == "Viewer")!;
        var email = "lockme@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Lock Me", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { (string)viewerRole["id"]! } })).EnsureSuccessStatusCode();

        var c = f.Client();
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Bad#Passw0rd1" })).StatusCode);
        var fifth = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Bad#Passw0rd1" });
        Assert.Equal((HttpStatusCode)423, fifth.StatusCode);
        var locked = await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Temp#Passw0rd" });
        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        var problem = await ApiFactory.Json(locked);
        Assert.Contains("Try again in", (string)problem["detail"]!);
        Assert.NotNull(problem["retryAt"]);
        Assert.True(locked.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Multi_workspace_user_picks_a_workspace_before_a_token_is_issued()
    {
        var c = f.Client();
        var res = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.OwnerEmail, password = ApiFactory.Password });
        var body = await ApiFactory.Json(res);
        Assert.True((bool)body["requiresWorkspaceSelection"]!);
        Assert.Null(body["accessToken"]);
        Assert.Equal(2, body["workspaces"]!.AsArray().Count);

        var (client, session) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        Assert.Equal("demo-re", (string?)session["workspace"]!["code"]);
        var me = await ApiFactory.Json(await client.GetAsync("/api/v1/me"));
        Assert.Contains(me["enabledModules"]!.AsArray(), m => (string?)m!["key"] == "realestate");

        // Switching issues a token for the other workspace and revokes the old one.
        var demoId = (string)session["workspaces"]!.AsArray().First(w => (string?)w!["code"] == "demo")!["id"]!;
        var oldToken = client.DefaultRequestHeaders.Authorization!.Parameter;
        var sw = await client.PostAsJsonAsync("/api/v1/auth/switch-workspace", new { workspaceId = demoId });
        Assert.Equal(HttpStatusCode.OK, sw.StatusCode);
        Assert.Equal("demo", (string?)(await ApiFactory.Json(sw))["workspace"]!["code"]);
        var stale = f.Client();
        stale.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_the_family()
    {
        var c = f.Client();
        var login = await c.PostAsJsonAsync("/api/v1/auth/login", new { email = ApiFactory.AdminEmail, password = ApiFactory.Password });
        var first = login.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("erp_rt=")).Split(';')[0];

        var r1 = await c.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var second = r1.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("erp_rt=")).Split(';')[0];
        Assert.NotEqual(first, second);

        var replay = f.CreateClient(new() { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        replay.DefaultRequestHeaders.Add("X-ERP-Client", "tests");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req.Headers.Add("Cookie", first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.SendAsync(req)).StatusCode);

        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        req2.Headers.Add("Cookie", second);
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.SendAsync(req2)).StatusCode); // family revoked
    }

    [Fact]
    public async Task Refresh_requires_the_client_header_and_a_cookie()
    {
        var bare = f.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsync("/api/v1/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Logout_denylists_the_access_token_and_kills_the_refresh_token()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsync("/api/v1/auth/refresh", null)).StatusCode);
    }

    [Fact]
    public async Task Tampered_or_unsigned_tokens_are_rejected()
    {
        var (c, s) = await f.SignInAsync(ApiFactory.AdminEmail);
        var token = (string)s["accessToken"]!;
        var parts = token.Split('.');
        var bad = f.Client();
        bad.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parts[0] + "." + parts[1] + "x." + parts[2]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bad.GetAsync("/api/v1/me")).StatusCode);
        var none = f.Client();
        none.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0." + parts[1] + ".");
        Assert.Equal(HttpStatusCode.Unauthorized, (await none.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Password_reset_token_is_single_use_and_policy_is_enforced()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var roles = await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"));
        var viewer = (string)roles.AsArray().First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        var email = "reset.me@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Reset Me", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { viewer } })).EnsureSuccessStatusCode();

        // Policy: 10+ chars, upper, lower, digit, not the email.
        var weak = await admin.PostAsJsonAsync("/api/v1/users", new { email = "weak@demo.local", fullName = "Weak", temporaryPassword = "short1A", roleIds = new[] { viewer } });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        var same = await admin.PostAsJsonAsync("/api/v1/users", new { email = "Same.As.Email1@demo.local", fullName = "Same", temporaryPassword = "same.as.email1@DEMO.local", roleIds = new[] { viewer } });
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);

        var c = f.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await c.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await c.PostAsJsonAsync("/api/v1/auth/forgot-password", new { email = "ghost@demo.local" })).StatusCode);
        var token = f.Resets.Tokens[email];
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "Brand#New2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "Other#New2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Brand#New2026" })).StatusCode);
    }
}

public class PlatformTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static object Onboard(string key, string industry, string country = "PK", string currency = "PKR", string timeZone = "Asia/Karachi") => new
    {
        workspaceName = $"QA {key}", signInKey = key, industry, countryCode = country, baseCurrency = currency, timeZone,
        legalEntityName = $"QA {key} Ltd", adminEmail = $"admin+{key}@example.test", adminName = "QA Admin", adminPassword = "Qa#Passw0rd1",
    };

    [Theory]
    [InlineData("qa-generic", "generic", new string[0])]
    [InlineData("qa-retail", "retail", new[] { "inventory" })]
    [InlineData("qa-re", "realestate", new[] { "realestate" })]
    public async Task Onboarding_enables_the_industry_bundle_and_neutral_seed(string key, string industry, string[] expected)
    {
        var res = await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", Onboard(key, industry));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var (c, _) = await f.SignInAsync($"admin+{key}@example.test", "Qa#Passw0rd1");

        var modules = (await ApiFactory.Json(await c.GetAsync("/api/v1/modules"))).AsArray();
        var enabled = modules.Where(m => (string?)m!["state"] == "enabled").Select(m => (string)m!["key"]!).ToList();
        Assert.Equal(expected.OrderBy(x => x), enabled.OrderBy(x => x));
        Assert.Contains(modules, m => (string?)m!["state"] == "unavailable"); // planned modules listed

        var taxes = (await ApiFactory.Json(await c.GetAsync("/api/v1/finance/tax-codes"))).AsArray();
        Assert.Contains(taxes, t => (string?)t!["code"] == "GST18"); // PK country pack, not UAE VAT
        var roles = (await ApiFactory.Json(await c.GetAsync("/api/v1/roles"))).AsArray().Select(r => (string)r!["name"]!).ToList();
        Assert.Equal(industry == "realestate", roles.Contains("Leasing manager"));
        Assert.Contains("Org admin", roles);
    }

    [Fact]
    public async Task Unknown_industry_is_rejected_with_allowed_values()
    {
        var res = await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", Onboard("qa-casino", "casino"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("generic", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Country_pack_adds_its_taxes_and_custom_fields_only_for_its_country()
    {
        Assert.Equal(HttpStatusCode.Created, (await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", Onboard("qa-uae", "generic", "AE", "AED", "Asia/Dubai"))).StatusCode);
        var (uae, _) = await f.SignInAsync("admin+qa-uae@example.test", "Qa#Passw0rd1");

        var taxes = (await ApiFactory.Json(await uae.GetAsync("/api/v1/finance/tax-codes"))).AsArray();
        Assert.Contains(taxes, t => (string?)t!["code"] == "VAT5");
        var form = (await ApiFactory.Json(await uae.GetAsync("/api/v1/custom-fields/form/party"))).AsArray();
        Assert.Contains(form, d => (string?)d!["key"] == "cf_ae_trn");
        Assert.Contains(form, d => (string?)d!["key"] == "cf_ae_emirates_id");

        // Pack fields are validated like any custom field (TRN is 15 digits).
        var bad = await uae.PostAsJsonAsync("/api/v1/parties", new { name = "Bad TRN LLC", kind = "Company", roles = "Customer", customFields = new { cf_ae_trn = "123" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var good = await uae.PostAsJsonAsync("/api/v1/parties", new { name = "Good TRN LLC", kind = "Company", roles = "Customer", customFields = new { cf_ae_trn = "100123456700003" } });
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);

        // Another country gets none of it.
        Assert.Equal(HttpStatusCode.Created, (await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", Onboard("qa-pk-pack", "generic"))).StatusCode);
        var (pk, _) = await f.SignInAsync("admin+qa-pk-pack@example.test", "Qa#Passw0rd1");
        var pkForm = (await ApiFactory.Json(await pk.GetAsync("/api/v1/custom-fields/form/party"))).AsArray();
        Assert.DoesNotContain(pkForm, d => ((string?)d!["key"])?.StartsWith("cf_ae_") == true);
    }

    [Fact]
    public async Task Industries_are_listed_anonymously()
    {
        var list = (await ApiFactory.Json(await f.Client().GetAsync("/api/v1/platform/industries"))).AsArray();
        Assert.Contains(list, i => (string?)i!["key"] == "realestate");
        Assert.Contains(list, i => (string?)i!["key"] == "generic");
    }

    [Fact]
    public async Task Disabled_module_returns_403_and_keeps_its_data()
    {
        var (c, s) = await f.SignInAsync(ApiFactory.AdminEmail); // "demo" is generic: inventory disabled
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var le = (string)me["legalEntities"]![0]!["id"]!;

        var blocked = await c.GetAsync("/api/v1/m/inventory/warehouses");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("module-disabled", await blocked.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/v1/modules/inventory/enable", null)).StatusCode);
        // Re-sign in so the token carries the module's permissions.
        (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new { code = "WH1", name = "Main", legalEntityId = le })).StatusCode);
        Assert.Single((await ApiFactory.Json(await c.GetAsync("/api/v1/m/inventory/warehouses"))).AsArray());

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/v1/modules/inventory/disable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/m/inventory/warehouses")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/v1/modules/inventory/enable", null)).StatusCode);
        Assert.Single((await ApiFactory.Json(await c.GetAsync("/api/v1/m/inventory/warehouses"))).AsArray()); // data retained
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/v1/modules/inventory/disable", null)).StatusCode);
    }

    [Fact]
    public async Task Menu_is_built_from_core_and_enabled_modules_only()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var menu = await c.GetStringAsync("/api/v1/navigation/menu");
        Assert.Contains("/admin/custom-fields", menu);
        Assert.DoesNotContain("/real-estate/", menu);
        var (re, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        Assert.Contains("/real-estate/properties", await re.GetStringAsync("/api/v1/navigation/menu"));
    }

    [Fact]
    public async Task Every_endpoint_declares_authorization_or_is_explicitly_anonymous()
    {
        var sources = f.Services.GetRequiredService<IEnumerable<EndpointDataSource>>();
        var missing = sources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAuthorizeData>() is null && e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(e => e.RoutePattern.RawText).ToList();
        Assert.True(missing.Count == 0, "Unprotected endpoints: " + string.Join(", ", missing));
    }
}

public class TenancyPermissionAndCustomFieldTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> CreateUserAsync(HttpClient admin, string email, string roleName)
    {
        var roles = await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"));
        var role = (string)roles.AsArray().First(r => (string?)r!["name"] == roleName)!["id"]!;
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = roleName, temporaryPassword = "Temp#Passw0rd", roleIds = new[] { role } })).EnsureSuccessStatusCode();
        return (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
    }

    [Fact]
    public async Task Records_of_another_workspace_are_invisible_and_org_in_body_is_ignored()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var created = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Isolated Co", kind = "Company", roles = "Customer", orgId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (string)(await ApiFactory.Json(created))["id"]!;
        Assert.StartsWith("PTY-", (string)(await ApiFactory.Json(await admin.GetAsync($"/api/v1/parties/{id}")))["code"]!);

        var (other, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/parties/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/parties/{id}", new { name = "Hijack", kind = "Company", roles = "Customer" })).StatusCode);
        var list = await other.GetStringAsync("/api/v1/parties?search=Isolated");
        Assert.DoesNotContain("Isolated Co", list);
    }

    [Fact]
    public async Task Entity_header_outside_the_workspace_is_forbidden()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        admin.DefaultRequestHeaders.Add("X-Entity-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/legal-entities")).StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_but_not_write_and_sees_masked_sensitive_values()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var p = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Bank Vendor", kind = "Company", roles = "Vendor", iban = "AE070331234567890123456", idNumber = "784-1990-1234567-1" });
        var id = (string)(await ApiFactory.Json(p))["id"]!;
        Assert.Equal("AE070331234567890123456", (string?)(await ApiFactory.Json(await admin.GetAsync($"/api/v1/parties/{id}")))["iban"]);

        var viewer = await CreateUserAsync(admin, "viewer1@demo.local", "Viewer");
        var seen = await ApiFactory.Json(await viewer.GetAsync($"/api/v1/parties/{id}"));
        Assert.EndsWith("3456", (string)seen["iban"]!);
        Assert.StartsWith("*", (string)seen["iban"]!);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/parties", new { name = "Nope", kind = "Company", roles = "Customer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/v1/custom-fields", new { entityType = "party", key = "cf_x", label = "X", dataType = "Text" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/users")).StatusCode);
    }

    [Fact]
    public async Task Maker_checker_blocks_self_approval_but_another_approver_can_decide()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var req = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/approvals", new { subject = "Buy laptops", amount = 5000 }));
        var id = (string)req["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/v1/approvals/{id}/approve", new { comment = "self" })).StatusCode);

        var approver = await CreateUserAsync(admin, "approver1@demo.local", "Approver");
        var ok = await approver.PostAsJsonAsync($"/api/v1/approvals/{id}/approve", new { comment = "fine" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var notes = await ApiFactory.Json(await admin.GetAsync("/api/v1/notifications"));
        Assert.True((int)notes["unread"]! >= 1);
    }

    [Fact]
    public async Task Custom_fields_are_defined_validated_stored_and_returned()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var def = await admin.PostAsJsonAsync("/api/v1/custom-fields", new
        {
            entityType = "party", key = "cf_vip_tier", label = "VIP tier", dataType = "Select", required = false,
            options = new { choices = new[] { new { value = "gold", label = "Gold" }, new { value = "silver", label = "Silver" } } },
            order = 1, section = "Loyalty", isSearchable = true, showInList = true,
        });
        Assert.Equal(HttpStatusCode.Created, def.StatusCode);
        var defId = (string)(await ApiFactory.Json(def))["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/v1/custom-fields", new { entityType = "party", key = "cf_vip_tier", label = "Again", dataType = "Text" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/custom-fields", new { entityType = "party", key = "Bad Key", label = "Bad", dataType = "Text" })).StatusCode);
        (await admin.PostAsJsonAsync("/api/v1/custom-fields", new { entityType = "party", key = "cf_since", label = "Customer since", dataType = "Date" })).EnsureSuccessStatusCode();

        var good = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Gold Co", kind = "Company", roles = "Customer", customFields = new { cf_vip_tier = "gold", cf_since = "2024-01-31" } });
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        var party = await ApiFactory.Json(good);
        Assert.Equal("gold", (string?)party["customFields"]!["cf_vip_tier"]);

        var bad = await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Bad Co", kind = "Company", roles = "Customer", customFields = new { cf_vip_tier = "platinum", cf_since = "31/01/2024", cf_nope = 1 } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ApiFactory.Json(bad))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("customFields.cf_vip_tier"));
        Assert.True(errors.ContainsKey("customFields.cf_since"));
        Assert.True(errors.ContainsKey("customFields.cf_nope"));

        // Generic values API + deactivation keeps data but hides it.
        var pid = (string)party["id"]!;
        var put = await admin.PutAsJsonAsync($"/api/v1/custom-fields/values/party/{pid}", new { cf_vip_tier = "silver" });
        Assert.Equal("silver", (string?)(await ApiFactory.Json(put))["cf_vip_tier"]);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/v1/custom-fields/{defId}")).StatusCode);
        var after = await ApiFactory.Json(await admin.GetAsync($"/api/v1/custom-fields/values/party/{pid}"));
        Assert.False(after.AsObject().ContainsKey("cf_vip_tier"));
        Assert.Equal("2024-01-31", (string?)after["cf_since"]);

        var form = (await ApiFactory.Json(await admin.GetAsync("/api/v1/custom-fields/form/party"))).AsArray();
        Assert.DoesNotContain(form, d => (string?)d!["key"] == "cf_vip_tier");

        // Another workspace does not see these definitions and cannot use the key.
        var (other, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        // (demo-re is a UAE workspace, so it has its own country-pack fields; only this test's definitions must be absent.)
        var otherDefs = (await ApiFactory.Json(await other.GetAsync("/api/v1/custom-fields?entityType=party"))).AsArray();
        Assert.DoesNotContain(otherDefs, d => (string?)d!["key"] is "cf_vip_tier" or "cf_since");
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/v1/parties", new { name = "X", kind = "Company", roles = "Customer", customFields = new { cf_since = "2024-01-31" } })).StatusCode);
    }

    [Fact]
    public async Task Writes_are_audited()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var p = await ApiFactory.Json(await admin.PostAsJsonAsync("/api/v1/parties", new { name = "Audited", kind = "Individual", roles = "Customer" }));
        var events = (await ApiFactory.Json(await admin.GetAsync($"/api/v1/audit?filter=recordType:eq:Party&filter=recordId:eq:{p["id"]}"))).AsArray();
        Assert.Contains(events, e => (string?)e!["action"] == "Created");
    }
}
