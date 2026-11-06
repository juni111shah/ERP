using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using ERP.Application.Assistant;
using ERP.Application.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>What the business modules add to the workspace assistant: every reader runs, returns only safe fields, and shows up only for people and workspaces it is meant for.</summary>
public class AssistantToolPacksTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static readonly string[] Unsafe = ["phone", "mobile", "email", "mail", "address", "iban", "bank", "contact", "passport", "salary"];

    private async Task EnableAsync(IEnumerable<string> modules)
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        foreach (var key in modules)
        {
            var enabled = await admin.PostAsync($"/api/v1/modules/{key}/enable", null);
            Assert.True(enabled.IsSuccessStatusCode, $"{key}: {await enabled.Content.ReadAsStringAsync()}");
        }
    }

    private List<string> RegisteredModules()
    {
        using var scope = f.Services.CreateScope();
        return scope.ServiceProvider.GetServices<IAssistantReader>().Select(r => r.ModuleKey).OfType<string>().Distinct().ToList();
    }

    /// <summary>A fresh sign-in: permissions travel in the token, so a newly enabled module takes a new one.</summary>
    private async Task<List<Claim>> AdminClaimsAsync()
    {
        var (_, session) = await f.SignInAsync(ApiFactory.AdminEmail);
        return new JwtSecurityTokenHandler().ReadJwtToken((string)session["accessToken"]!).Claims.ToList();
    }

    /// <summary>The same claims with other permissions, so the person stays in the same workspace.</summary>
    private static List<Claim> WithPermissions(IEnumerable<Claim> claims, params string[] permissions)
        => claims.Where(c => c.Type != ErpClaims.Permission).Concat(permissions.Select(p => new Claim(ErpClaims.Permission, p))).ToList();

    /// <summary>A request scope acting as this person. Not async: the current request only reaches code the caller awaits if it is set on the caller's own flow.</summary>
    private IServiceScope ActingAs(IEnumerable<Claim> claims)
    {
        var scope = f.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")), RequestServices = scope.ServiceProvider };
        return scope;
    }

    private static Type RowType(object rows)
    {
        Assert.IsAssignableFrom<IEnumerable>(rows);
        var row = rows.GetType().GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).GetGenericArguments()[0];
        Assert.NotEqual(typeof(object), row);
        return row;
    }

    [Fact]
    public async Task Every_reader_runs_and_returns_only_safe_fields()
    {
        var modules = RegisteredModules();
        Assert.DoesNotContain("hr", modules);
        await EnableAsync(modules);
        var claims = await AdminClaimsAsync();

        using var scope = ActingAs(claims);
        var readers = scope.ServiceProvider.GetServices<IAssistantReader>().ToList();
        Assert.Equal(readers.Count, readers.Select(r => r.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var expected in new[] { "purchase_orders", "purchase_requisitions", "stock_on_hand", "stock_counts", "production_work_orders", "bills_of_materials", "projects", "project_tasks", "properties", "property_units", "leases", "property_work_orders", "service_engagements", "service_retainers", "distribution_routes", "van_trips", "retail_stores", "retail_promotions", "pos_registers", "pos_orders", "crm_leads", "crm_opportunities" })
            Assert.Contains(readers, r => r.Key == expected);

        foreach (var reader in readers)
        {
            foreach (var search in new[] { null, "x" })
            {
                object rows;
                try { rows = await reader.ListAsync(search, 5, default); }
                catch (Exception e) { throw new Xunit.Sdk.XunitException($"{reader.Key} (search {search ?? "none"}): {e}"); }
                var names = RowType(rows).GetProperties().Select(p => p.Name).ToList();
                Assert.NotEmpty(names);
                var bad = names.Where(n => Unsafe.Any(u => n.Contains(u, StringComparison.OrdinalIgnoreCase))).ToList();
                Assert.True(bad.Count == 0, $"{reader.Key} returns {string.Join(", ", bad)}");
            }
        }
    }

    [Fact]
    public async Task A_person_without_the_permission_does_not_get_the_reader()
    {
        await EnableAsync(RegisteredModules());
        var claims = await AdminClaimsAsync();

        using (var scope = ActingAs(WithPermissions(claims, "assistant.chats.use")))
        {
            var catalog = scope.ServiceProvider.GetRequiredService<AssistantCatalog>();
            Assert.Empty(await catalog.ReadersAsync(default));
            Assert.Null(await catalog.FindReaderAsync("purchase_orders", default));
        }

        using (var scope = ActingAs(WithPermissions(claims, "purchasing.orders.read")))
        {
            var catalog = scope.ServiceProvider.GetRequiredService<AssistantCatalog>();
            Assert.Equal("purchase_orders", Assert.Single(await catalog.ReadersAsync(default)).Key);
            Assert.Null(await catalog.FindReaderAsync("purchase_requisitions", default));
        }
    }

    [Fact]
    public async Task A_disabled_module_hides_its_readers_and_help()
    {
        await EnableAsync(["retail"]);
        var claims = await AdminClaimsAsync();
        const string question = "How do I create a promotion?";

        using (var scope = ActingAs(claims))
        {
            var catalog = scope.ServiceProvider.GetRequiredService<AssistantCatalog>();
            Assert.Contains(await catalog.FindHelpAsync(question, 3, default), a => a.Title == "Create a promotion");
        }

        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        (await admin.PostAsync("/api/v1/modules/retail/disable", null)).EnsureSuccessStatusCode();
        try
        {
            using var scope = ActingAs(claims);
            var catalog = scope.ServiceProvider.GetRequiredService<AssistantCatalog>();
            Assert.DoesNotContain(await catalog.FindHelpAsync(question, 3, default), a => a.Title == "Create a promotion");
            Assert.DoesNotContain(await catalog.ReadersAsync(default), r => r.ModuleKey == "retail");
        }
        finally { (await admin.PostAsync("/api/v1/modules/retail/enable", null)).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Help_a_person_cannot_open_is_hidden_and_core_help_shows_without_a_module()
    {
        await EnableAsync(["retail"]);
        var claims = await AdminClaimsAsync();

        using var scope = ActingAs(WithPermissions(claims, "assistant.chats.use"));
        var catalog = scope.ServiceProvider.GetRequiredService<AssistantCatalog>();
        Assert.DoesNotContain(await catalog.FindHelpAsync("How do I create a promotion?", 3, default), a => a.Title == "Create a promotion");
        Assert.Contains(await catalog.FindHelpAsync("How do I switch legal entity?", 3, default), a => a.Title == "Switch legal entity");
    }

    [Fact]
    public async Task Members_only_projects_hide_projects_and_their_tasks_from_others()
    {
        await EnableAsync(["projects"]);
        var (admin, session) = await f.SignInAsync(ApiFactory.AdminEmail);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken((string)session["accessToken"]!).Claims.ToList();
        var name = "Secret project " + Guid.NewGuid().ToString("N")[..8];
        var entities = (await ApiFactory.Json(await admin.GetAsync("/api/v1/legal-entities"))).AsArray();
        var created = await admin.PostAsJsonAsync("/api/v1/m/projects/projects", new { legalEntityId = (string)entities[0]!["id"]!, name });
        Assert.True(created.IsSuccessStatusCode, await created.Content.ReadAsStringAsync());
        var project = await ApiFactory.Json(created);
        var task = await admin.PostAsJsonAsync("/api/v1/m/projects/tasks", new { projectId = (string)project["id"]!, title = name + " task" });
        Assert.True(task.IsSuccessStatusCode, await task.Content.ReadAsStringAsync());

        async Task<string> SeenAsync(string key, params string[] permissions)
        {
            using var scope = ActingAs(WithPermissions(claims, permissions));
            var reader = await scope.ServiceProvider.GetRequiredService<AssistantCatalog>().FindReaderAsync(key, default);
            return System.Text.Json.JsonSerializer.Serialize(await reader!.ListAsync(name, 5, default));
        }

        Assert.Contains(name, await SeenAsync("projects", "projects.projects.read"));
        Assert.Contains(name, await SeenAsync("project_tasks", "projects.tasks.read"));

        var on = await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, bool> { ["projects.restrictToMembers"] = true });
        Assert.True(on.IsSuccessStatusCode, await on.Content.ReadAsStringAsync());
        try
        {
            Assert.DoesNotContain(name, await SeenAsync("projects", "projects.projects.read"));
            Assert.DoesNotContain(name, await SeenAsync("project_tasks", "projects.tasks.read"));
        }
        finally { (await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, bool> { ["projects.restrictToMembers"] = false })).EnsureSuccessStatusCode(); }
    }
}
