using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Vendor portal: email-code sign-in for vendors, isolation between vendors and between portal kinds, bills/payments/statement and work-order updates.</summary>
public class VendorPortalTests(ApiFactory f) : IClassFixture<ApiFactory>
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

    private sealed record Vendor(string PartyId, string Email, string BillId, decimal BillTotal);
    private sealed record Ctx(HttpClient Staff, string Entity, string Bank, string Expense, string PropertyId);

    private async Task<Ctx> StaffAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = U("VTower "), propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1m }), HttpStatusCode.Created);
        return new Ctx(c, entity, (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!, (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!, (string)prop["id"]!);
    }

    private async Task<Vendor> VendorAsync(Ctx x, decimal price, string roles = "Vendor")
    {
        var email = U("vendor") + "@example.com";
        var party = await Ok(await x.Staff.PostAsJsonAsync("/api/v1/parties", new { name = U("Vendor "), kind = "Company", roles, email }), HttpStatusCode.Created);
        var bill = await Ok(await x.Staff.PostAsJsonAsync($"{Fin}/bills", new
        {
            legalEntityId = x.Entity, partyId = (string)party["id"]!, vendorReference = U("INV-"), billDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd"),
            lines = new[] { new { description = "Service", quantity = 1m, price, accountId = x.Expense } },
        }), HttpStatusCode.Created);
        var posted = await Ok(await x.Staff.PostAsync($"{Fin}/bills/{(string)bill["id"]!}/post", null));
        return new Vendor((string)party["id"]!, email, (string)posted["id"]!, (decimal)posted["total"]!);
    }

    private async Task<HttpClient> SignInAsync(string email, string? kind = null)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email, kind })).StatusCode);
        var code = f.Emails.LastCodeFor(email);
        Assert.NotNull(code);
        var res = await Ok(await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email, code, kind }));
        var client = f.Client();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (string)res["accessToken"]!);
        return client;
    }

    [Fact]
    public async Task A_vendor_signs_in_and_sees_only_their_own_bills_payments_and_statement()
    {
        var x = await StaffAsync();
        var a = await VendorAsync(x, 1000m);
        var b = await VendorAsync(x, 700m);
        await Ok(await x.Staff.PostAsJsonAsync($"{Fin}/payments", new
        {
            legalEntityId = x.Entity, partyId = a.PartyId, date = DateTime.UtcNow.ToString("yyyy-MM-dd"), method = "bankTransfer", bankAccountId = x.Bank, amount = 400m,
            reference = "PAY-V1", allocations = new[] { new { billId = a.BillId, amount = 400m } },
        }), HttpStatusCode.Created);

        var portal = await SignInAsync(a.Email); // no kind given: not a customer, so the vendor is found
        var me = await Ok(await portal.GetAsync($"{Portal}/me"));
        Assert.Equal(a.PartyId, (string)me["partyId"]!);

        var bills = (await Ok(await portal.GetAsync($"{Portal}/vendor/bills"))).AsArray();
        var bill = Assert.Single(bills)!;
        Assert.Equal(a.BillId, (string)bill["id"]!);
        Assert.Equal(a.BillTotal - 400m, (decimal)bill["outstanding"]!);
        Assert.DoesNotContain(bills, r => (string)r!["id"]! == b.BillId);

        var payments = (await Ok(await portal.GetAsync($"{Portal}/vendor/payments"))).AsArray();
        Assert.Equal(400m, (decimal)Assert.Single(payments)!["amount"]!);

        var statement = await Ok(await portal.GetAsync($"{Portal}/vendor/statement"));
        Assert.Equal(a.BillTotal - 400m, (decimal)statement["balance"]!);
    }

    [Fact]
    public async Task Customer_and_vendor_tokens_cannot_use_each_others_routes_or_staff_routes()
    {
        var x = await StaffAsync();
        var v = await VendorAsync(x, 500m);
        var vendor = await SignInAsync(v.Email, "vendor");
        Assert.Equal(HttpStatusCode.Forbidden, (await vendor.GetAsync($"{Portal}/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendor.GetAsync($"{Portal}/statement")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendor.GetAsync($"{Re}/portal/leases")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vendor.GetAsync($"{Fin}/bills")).StatusCode);

        var tenantEmail = U("tenant") + "@example.com";
        await Ok(await x.Staff.PostAsJsonAsync("/api/v1/parties", new { name = U("Tenant "), kind = "Individual", roles = "Customer", email = tenantEmail }), HttpStatusCode.Created);
        var customer = await SignInAsync(tenantEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"{Portal}/vendor/bills")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"{Re}/portal/vendor/work-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"{Portal}/invoices")).StatusCode);

        // a staff token is not a vendor token, and no token means no access
        Assert.Equal(HttpStatusCode.Forbidden, (await x.Staff.GetAsync($"{Portal}/vendor/bills")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{Portal}/vendor/bills")).StatusCode);
    }

    [Fact]
    public async Task The_kind_chooses_between_a_customer_and_a_vendor_that_share_an_email_and_unknown_kinds_are_rejected()
    {
        var x = await StaffAsync();
        var email = U("both") + "@example.com";
        await Ok(await x.Staff.PostAsJsonAsync("/api/v1/parties", new { name = U("Both "), kind = "Company", roles = "Customer, Vendor", email }), HttpStatusCode.Created);
        var asDefault = await SignInAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await asDefault.GetAsync($"{Portal}/invoices")).StatusCode); // customer preferred
        var asVendor = await SignInAsync(email, "vendor");
        Assert.Equal(HttpStatusCode.OK, (await asVendor.GetAsync($"{Portal}/vendor/bills")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email, kind = "admin" })).StatusCode);

        // a vendor cannot sign in as a customer, and wrong or unknown codes fail
        var onlyVendor = U("onlyv") + "@example.com";
        await Ok(await x.Staff.PostAsJsonAsync("/api/v1/parties", new { name = U("OV "), kind = "Company", roles = "Vendor", email = onlyVendor }), HttpStatusCode.Created);
        await f.Client().PostAsJsonAsync($"{Portal}/auth/request-code", new { workspace = "demo-re", email = onlyVendor, kind = "customer" });
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().PostAsJsonAsync($"{Portal}/auth/verify", new { workspace = "demo-re", email = onlyVendor, code = "123456", kind = "customer" })).StatusCode);
    }

    [Fact]
    public async Task A_vendor_can_progress_only_their_own_work_orders_within_the_allowed_statuses()
    {
        var x = await StaffAsync();
        var a = await VendorAsync(x, 100m);
        var b = await VendorAsync(x, 100m);
        var order = await Ok(await x.Staff.PostAsJsonAsync($"{Re}/work-orders", new { propertyId = x.PropertyId, issue = "Fix the pump", priority = "normal", vendorPartyId = a.PartyId, estimatedCost = 300m }), HttpStatusCode.Created);
        var id = (string)order["id"]!;
        await Ok(await x.Staff.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "assigned" }));

        var va = await SignInAsync(a.Email, "vendor");
        var vb = await SignInAsync(b.Email, "vendor");
        Assert.Contains((await Ok(await va.GetAsync($"{Re}/portal/vendor/work-orders"))).AsArray(), o => (string)o!["id"]! == id);
        Assert.DoesNotContain((await Ok(await vb.GetAsync($"{Re}/portal/vendor/work-orders"))).AsArray(), o => (string)o!["id"]! == id);
        Assert.Equal(HttpStatusCode.NotFound, (await vb.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "inProgress" })).StatusCode);

        // vendors cannot close or cancel; only start, hold or complete
        Assert.Equal(HttpStatusCode.BadRequest, (await va.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "closed" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await va.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "cancelled" })).StatusCode);

        var started = await Ok(await va.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "inProgress", notes = "On site" }));
        Assert.Equal("inProgress", (string)started["status"]!);
        var done = await Ok(await va.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "completed", actualCost = 280m, notes = "Pump replaced" }));
        Assert.Equal("completed", (string)done["status"]!);
        Assert.Equal(280m, (decimal)done["actualCost"]!);
        var staffView = await Ok(await x.Staff.GetAsync($"{Re}/work-orders/{id}"));
        Assert.Equal("Pump replaced", (string)staffView["resolutionNotes"]!);

        // illegal transition: completed cannot go to on hold
        Assert.Equal(HttpStatusCode.Conflict, (await va.PostAsJsonAsync($"{Re}/portal/vendor/work-orders/{id}/update", new { status = "onHold" })).StatusCode);
    }
}
