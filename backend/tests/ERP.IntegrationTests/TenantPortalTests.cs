using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Tenant portal: email-code sign-in, isolation between tenants, read-only finance views, lease schedule and maintenance requests.</summary>
public class TenantPortalTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";
    private const string Portal = "/api/v1/portal";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private sealed record Tenant(HttpClient Staff, string Entity, string Bank, string PartyId, string Email, string LeaseId);

    private async Task<Tenant> TenantAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = U("Tower "), propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1_000_000m }), HttpStatusCode.Created);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "7", unitTypeId = (string)type["id"]!, areaSqft = 900, askingRent = 100_000m }), HttpStatusCode.Created);
        var email = U("tenant") + "@example.com";
        var party = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = U("Tenant "), kind = "Individual", roles = "Customer", email }), HttpStatusCode.Created);
        var start = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd");
        var end = DateTime.UtcNow.AddMonths(11).ToString("yyyy-MM-dd");
        var lease = await Ok(await c.PostAsJsonAsync($"{Re}/leases", new { unitId = (string)unit["id"]!, lesseePartyId = (string)party["id"]!, startDate = start, endDate = end, annualRent = 100_000m, paymentFrequency = 4, securityDepositAmount = 5_000m, activateNow = true }), HttpStatusCode.Created);
        return new Tenant(c, entity, bank, (string)party["id"]!, email, (string)lease["id"]!);
    }

    private async Task<HttpClient> SignInAsync(string email, string workspace = "demo-re")
    {
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace, email })).StatusCode);
        var code = f.Emails.LastCodeFor(email);
        Assert.NotNull(code);
        var res = await Ok(await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace, email, code }));
        var client = f.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)res["accessToken"]!);
        return client;
    }

    [Fact]
    public async Task A_tenant_signs_in_with_an_emailed_code_and_sees_only_their_own_records()
    {
        var a = await TenantAsync();
        var b = await TenantAsync();
        // bill the lessee so there is something to see
        await Ok(await a.Staff.PostAsJsonAsync($"{Re}/billing/run", new { asOf = DateTime.UtcNow.AddMonths(3).ToString("yyyy-MM-dd"), legalEntityId = a.Entity }));

        var portal = await SignInAsync(a.Email);
        var me = await Ok(await portal.GetAsync($"{Portal}/me"));
        Assert.Equal(a.PartyId, (string)me["partyId"]!);
        var invoices = (await Ok(await portal.GetAsync($"{Portal}/invoices"))).AsArray();
        Assert.NotEmpty(invoices);
        Assert.All(invoices, i => Assert.Equal(0, ((decimal)i!["paid"]!)));
        var statement = await Ok(await portal.GetAsync($"{Portal}/statement"));
        Assert.True((decimal)statement["balance"]! > 0);

        var leases = (await Ok(await portal.GetAsync($"{Re}/portal/leases"))).AsArray();
        var lease = Assert.Single(leases);
        Assert.Equal(a.LeaseId, (string)lease!["id"]!);
        Assert.NotEmpty(lease["schedule"]!.AsArray());

        // tenant B's lease is invisible, and the portal token cannot use staff endpoints
        Assert.DoesNotContain(leases, l => (string)l!["id"]! == b.LeaseId);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync($"{Fin}/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync($"{Re}/leases")).StatusCode);
        // a staff token is not a portal token
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Staff.GetAsync($"{Portal}/me")).StatusCode);
    }

    [Fact]
    public async Task Wrong_codes_unknown_emails_and_reused_codes_are_rejected_without_leaking_which_exists()
    {
        var t = await TenantAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email = "nobody@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email = "nobody@example.com", code = "123456" })).StatusCode);

        await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email = t.Email });
        var code = f.Emails.LastCodeFor(t.Email)!;
        var wrong = code == "111111" ? "222222" : "111111";
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email = t.Email, code = wrong })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email = t.Email, code })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email = t.Email, code })).StatusCode); // single use
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Portal}/me")).StatusCode);
    }

    [Fact]
    public async Task A_tenant_can_raise_a_maintenance_request_that_staff_see_as_a_work_order()
    {
        var t = await TenantAsync();
        var portal = await SignInAsync(t.Email);
        var created = await Ok(await portal.PostAsJsonAsync($"{Re}/portal/requests", new { leaseId = t.LeaseId, issue = "Kitchen tap leaking", category = "plumbing", priority = "high" }), HttpStatusCode.Created);
        Assert.Equal("new", (string)created["status"]!);
        Assert.Contains((await Ok(await portal.GetAsync($"{Re}/portal/requests"))).AsArray(), r => (string)r!["id"]! == (string)created["id"]!);
        var order = await Ok(await t.Staff.GetAsync($"{Re}/work-orders/{(string)created["id"]!}"));
        Assert.Equal(t.LeaseId, (string)order["leaseId"]!);
        Assert.Contains("tenant portal", (string)order["reportedBy"]!);
        // another lease is not theirs
        var other = await TenantAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await portal.PostAsJsonAsync($"{Re}/portal/requests", new { leaseId = other.LeaseId, issue = "x" })).StatusCode);
    }

    [Fact]
    public async Task Portal_token_is_forbidden_from_staff_endpoints_guarded_only_by_authentication()
    {
        var a = await TenantAsync();
        var portal = await SignInAsync(a.Email);

        // Acceptance criteria: Portal token gets 403 on POST /api/v1/approvals, GET /api/v1/dashboard, GET /api/v1/search, GET /api/v1/me
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.PostAsJsonAsync("/api/v1/approvals", new { scope = "test", title = "test" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/search?q=test")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await portal.GetAsync("/api/v1/me")).StatusCode);

        // Portal token still works on /api/v1/portal/*
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"{Portal}/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await portal.GetAsync($"{Portal}/invoices")).StatusCode);

        // Staff tokens behave as before
        Assert.Equal(HttpStatusCode.OK, (await a.Staff.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Staff.GetAsync("/api/v1/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.Staff.GetAsync("/api/v1/search?q=test")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Staff.GetAsync($"{Portal}/me")).StatusCode);
    }
}
