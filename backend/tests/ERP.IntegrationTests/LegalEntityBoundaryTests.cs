using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Domain.Finance;
using ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>The company boundary on writes and approvals: a caller limited to company A cannot write into or decide for company B. Trusted code is not limited.</summary>
public class LegalEntityBoundaryTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pw = "Temp#Passw0rd";

    private sealed record Setup(HttpClient Admin, HttpClient Restricted, string A, string B, string Client, string Engagement);

    private async Task<Setup> StartAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        foreach (var m in new[] { "services", "retail", "distribution", "pos" }) await first.PostAsync($"/api/v1/modules/{m}/enable", null);
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var a = await EntityAsync(admin, "Company A");
        var b = await EntityAsync(admin, "Company B");
        var client = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/parties", new { name = HrKit.Unique("Client"), kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var engagement = (string)(await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/m/services/engagements", EngagementBody(a, client)), HttpStatusCode.Created))["id"]!;

        var adminRole = (string)(await HrKit.Ok(await admin.GetAsync("/api/v1/roles?pageSize=200"))).AsArray().First(r => (string)r!["name"]! == "Org admin")!["id"]!;
        var email = HrKit.Unique("restricted") + "@demo.local";
        await HrKit.Ok(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Company A Only", temporaryPassword = Pw, roleIds = new[] { adminRole }, legalEntityIds = new[] { a } }), HttpStatusCode.Created);
        var (restricted, _) = await f.SignInAsync(email, Pw);
        return new Setup(admin, restricted, a, b, client, engagement);
    }

    private static async Task<string> EntityAsync(HttpClient c, string label)
        => (string)(await HrKit.Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = HrKit.Unique(label), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    private static object EngagementBody(string entity, string client)
        => new { legalEntityId = entity, title = HrKit.Unique("Engagement"), clientId = client, clientName = "Client", type = 1, startDate = "2026-05-01" };

    /// <summary>Every command named in the finding, for the given company.</summary>
    private static IEnumerable<(string Url, object Body)> Commands(Setup s, string entity)
    {
        var day = "2026-05-01";
        yield return ("/api/v1/m/services/engagements", EngagementBody(entity, s.Client));
        yield return ("/api/v1/m/services/retainers", new { legalEntityId = entity, engagementId = s.Engagement, clientId = s.Client, clientName = "Client", title = HrKit.Unique("Retainer"), cycle = 1, recurringFee = 100m, includedHours = 1m, overageHourlyRate = 1m, startDate = day });
        yield return ("/api/v1/m/services/milestones", new { legalEntityId = entity, engagementId = s.Engagement, title = HrKit.Unique("Milestone"), amount = 100m, dueDate = day });
        yield return ("/api/v1/m/services/time-entries", new { legalEntityId = entity, engagementId = s.Engagement, consultantUserId = Guid.NewGuid(), consultantName = "Pat", activityDate = day, hours = 1m, hourlyRate = 1m, taskDescription = "Work", isBillable = true });
        yield return ("/api/v1/m/retail/stores", new { legalEntityId = entity, code = HrKit.Unique("S"), name = "Store", isActive = true });
        yield return ("/api/v1/m/retail/pricelists", new { legalEntityId = entity, code = HrKit.Unique("PL"), name = "Prices", type = "Standard", currency = "AED", items = Array.Empty<object>() });
        yield return ("/api/v1/m/retail/promotions", new { legalEntityId = entity, code = HrKit.Unique("P"), name = "Promo", type = 0, discountValue = 5m, startDate = day, endDate = day, isActive = true });
        yield return ("/api/v1/m/distribution/routes", new { legalEntityId = entity, code = HrKit.Unique("R"), name = "Route", isActive = true });
        yield return ("/api/v1/m/distribution/wholesale", new { legalEntityId = entity, itemId = Guid.NewGuid(), itemCode = "X", itemName = "X", tier = 0, minQuantity = 1m, discountPercentage = 0m, wholesaleUnitPrice = 1m, isActive = true });
        yield return ("/api/v1/m/pos/registers", new { legalEntityId = entity, code = HrKit.Unique("REG"), name = "Till" });
    }

    [Fact]
    public async Task A_restricted_user_cannot_create_in_another_company_with_any_of_the_named_commands()
    {
        var s = await StartAsync();
        foreach (var (url, body) in Commands(s, s.B))
        {
            var res = await s.Restricted.PostAsJsonAsync(url, body);
            var text = await res.Content.ReadAsStringAsync();
            Assert.True(res.StatusCode == HttpStatusCode.BadRequest && text.Contains("legalEntityId", StringComparison.OrdinalIgnoreCase), $"{url}: {res.StatusCode} {text}");
        }
    }

    [Fact]
    public async Task A_legal_entity_that_does_not_exist_is_refused_even_for_an_unrestricted_user()
    {
        var s = await StartAsync();
        foreach (var (url, body) in Commands(s, Guid.NewGuid().ToString()))
        {
            var res = await s.Admin.PostAsJsonAsync(url, body);
            var text = await res.Content.ReadAsStringAsync();
            Assert.True(res.StatusCode == HttpStatusCode.BadRequest && text.Contains("legalEntityId", StringComparison.OrdinalIgnoreCase), $"{url}: {res.StatusCode} {text}");
        }
    }

    [Fact]
    public async Task A_restricted_user_keeps_working_in_their_own_company()
    {
        var s = await StartAsync();
        foreach (var (url, body) in Commands(s, s.A))
        {
            var res = await s.Restricted.PostAsJsonAsync(url, body);
            Assert.True(res.StatusCode == HttpStatusCode.Created, $"{url}: {res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
    }

    [Fact]
    public async Task The_save_interceptor_blocks_rows_of_another_company_but_not_trusted_code()
    {
        var s = await StartAsync();
        using var scope = f.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var (a, b) = (Guid.Parse(s.A), Guid.Parse(s.B));
        var orgId = await db.LegalEntities.IgnoreQueryFilters().Where(e => e.Id == a).Select(e => e.OrgId).FirstAsync();
        JournalEntry Journal(Guid entity) => new() { LegalEntityId = entity, Number = HrKit.Unique("BOUND"), Date = new DateOnly(2026, 5, 1), CurrencyCode = "AED" };

        var accessor = services.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ErpClaims.Org, orgId.ToString()), new Claim(ErpClaims.Entities, s.A)], "test")) };
        try
        {
            db.Add(Journal(b));
            await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var own = Journal(a);
            db.Add(own);
            await db.SaveChangesAsync();

            own.LegalEntityId = b; // moved across the boundary
            await Assert.ThrowsAsync<ForbiddenException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            // background jobs, onboarding and the like switch to the workspace explicitly and have no restricted list
            using (services.GetRequiredService<IOrgContext>().Use(orgId))
            {
                db.Add(Journal(b));
                await db.SaveChangesAsync();
            }
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task A_restricted_approver_neither_sees_nor_decides_another_companys_requests()
    {
        var s = await StartAsync();
        async Task<string> SubmitAsync(string subject, string? entity)
            => (string)(await HrKit.Ok(await s.Admin.PostAsJsonAsync("/api/v1/approvals", new { subject, legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var ofA = await SubmitAsync(HrKit.Unique("Of A"), s.A);
        var ofB = await SubmitAsync(HrKit.Unique("Of B"), s.B);
        var ofNone = await SubmitAsync(HrKit.Unique("Of none"), null);

        var listed = (await HrKit.Ok(await s.Restricted.GetAsync("/api/v1/approvals"))).AsArray().Select(r => (string)r!["id"]!).ToList();
        Assert.Contains(ofA, listed);
        Assert.Contains(ofNone, listed);
        Assert.DoesNotContain(ofB, listed);

        Assert.Equal(HttpStatusCode.NotFound, (await s.Restricted.PostAsJsonAsync($"/api/v1/approvals/{ofB}/approve", new { comment = "No" })).StatusCode);
        var still = (await HrKit.Ok(await s.Admin.GetAsync("/api/v1/approvals?scope=mine"))).AsArray().First(r => (string)r!["id"]! == ofB)!;
        Assert.Equal("pending", ((string)still["status"]!).ToLowerInvariant());

        await HrKit.Ok(await s.Restricted.PostAsJsonAsync($"/api/v1/approvals/{ofA}/approve", new { comment = "Yes" }));

        // and cannot raise one against that company either
        var submit = await s.Restricted.PostAsJsonAsync("/api/v1/approvals", new { subject = "Into B", legalEntityId = s.B });
        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Admin.PostAsJsonAsync("/api/v1/approvals", new { subject = "Nowhere", legalEntityId = Guid.NewGuid() })).StatusCode);
    }
}
