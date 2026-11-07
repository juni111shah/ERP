using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>The record engine and business configuration, driven through the public API.</summary>
public class IndustryPackTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Expected {expected} but got {r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data");
    }

    /// <summary>A new workspace of the given business type, with an administrator signed in.</summary>
    private async Task<(HttpClient Client, Guid Entity, string Email)> WorkspaceAsync(string industry)
    {
        var key = "t-" + Guid.NewGuid().ToString("N")[..10];
        var email = key + "@example.test";
        var created = await factory.Client().PostAsJsonAsync("/api/v1/platform/onboarding", new
        {
            workspaceName = "Test " + key, signInKey = key, industry, countryCode = "AE", baseCurrency = "AED", timeZone = "UTC", legalEntityName = "Test " + key,
            adminEmail = email, adminName = "Admin", adminPassword = ApiFactory.Password,
        });
        var result = await Data(created, HttpStatusCode.Created);
        var (client, _) = await factory.SignInAsync(email);
        return (client, result.GetProperty("legalEntityId").GetGuid(), email);
    }

    /// <summary>A second person in the workspace with the given roles, signed in.</summary>
    private async Task<HttpClient> UserAsync(HttpClient admin, params Guid[] roles)
    {
        var email = "u-" + Guid.NewGuid().ToString("N")[..10] + "@example.test";
        await Data(await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Second Person", temporaryPassword = ApiFactory.Password, roleIds = roles }), HttpStatusCode.Created);
        var (client, _) = await factory.SignInAsync(email);
        return client;
    }

    private async Task<HttpClient> SecondAdminAsync(HttpClient admin)
    {
        var roles = await Data(await admin.GetAsync("/api/v1/roles"));
        var list = roles.ValueKind == JsonValueKind.Array ? roles : roles.GetProperty("items");
        return await UserAsync(admin, list.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Org admin").GetProperty("id").GetGuid());
    }

    private static async Task<Guid> PartyAsync(HttpClient c, string name, string roles)
        => (await Data(await c.PostAsJsonAsync("/api/v1/parties", new { name, kind = "Company", roles }), HttpStatusCode.Created)).GetProperty("id").GetGuid();

    private static async Task<Guid> SysAccountAsync(HttpClient c, string role)
    {
        var accounts = await Data(await c.GetAsync("/api/v1/finance/accounts?pageSize=300"));
        var list = accounts.ValueKind == JsonValueKind.Array ? accounts : accounts.GetProperty("items");
        return list.EnumerateArray().First(a => a.TryGetProperty("systemRole", out var r) && r.ValueKind == JsonValueKind.String && r.GetString() == role).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_business_type_decides_modules_menu_words_units_and_dashboard_order()
    {
        var (c, _, _) = await WorkspaceAsync("health-clinic");
        var profile = await Data(await c.GetAsync("/api/v1/setup/profile"));
        Assert.Equal("health-clinic", profile.GetProperty("industryCode").GetString());
        Assert.Equal("Healthcare", profile.GetProperty("industryGroup").GetString());
        Assert.Contains("visit", profile.GetProperty("units").EnumerateArray().Select(u => u.GetString()));
        Assert.Equal(0, profile.GetProperty("missingModules").GetArrayLength());

        var menu = (await Data(await c.GetAsync("/api/v1/navigation/menu"))).EnumerateArray().ToList();
        var groups = menu.Select(g => g.GetProperty("id").GetString()).ToList();
        Assert.Contains("healthcare", groups);
        Assert.Contains("inventory", groups.Concat(menu.SelectMany(g => g.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("module").GetString()))));
        Assert.DoesNotContain("logistics", groups);
        Assert.DoesNotContain("education", groups);
        var customers = menu.SelectMany(g => g.GetProperty("items").EnumerateArray()).First(i => i.GetProperty("id").GetString() == "sales.customers");
        Assert.Equal("Billing accounts", customers.GetProperty("label").GetString());
    }

    [Fact]
    public async Task A_module_of_another_industry_stays_closed()
    {
        var (c, entity, _) = await WorkspaceAsync("edu-school");
        var r = await c.PostAsJsonAsync("/api/v1/m/logistics/records/vehicles", new { legalEntityId = entity, title = "Van", recordDate = "2026-01-05", data = new { plate = "A1", kind = "Van" } });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Fields_statuses_and_references_follow_the_definition()
    {
        var (c, entity, _) = await WorkspaceAsync("log-cargo");
        const string Vehicles = "/api/v1/m/logistics/records/vehicles";

        var missing = await c.PostAsJsonAsync(Vehicles, new { legalEntityId = entity, title = "Van 1", recordDate = "2026-01-05", data = new { kind = "Van" } });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var wrongChoice = await c.PostAsJsonAsync(Vehicles, new { legalEntityId = entity, title = "Van 1", recordDate = "2026-01-05", data = new { plate = "A1", kind = "Boat" } });
        Assert.Equal(HttpStatusCode.BadRequest, wrongChoice.StatusCode);

        var vehicle = await Data(await c.PostAsJsonAsync(Vehicles, new { legalEntityId = entity, title = "Van 1", recordDate = "2026-01-05", data = new { plate = "A1", kind = "Van", extra = "dropped" } }), HttpStatusCode.Created);
        Assert.Equal("active", vehicle.GetProperty("status").GetString());
        Assert.False(vehicle.GetProperty("data").TryGetProperty("extra", out _));

        var bad = await c.PostAsJsonAsync($"{Vehicles}/{vehicle.GetProperty("id").GetGuid()}/transition", new { to = "cancelled" });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        var noReason = await c.PostAsJsonAsync($"{Vehicles}/{vehicle.GetProperty("id").GetGuid()}/transition", new { to = "inactive" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var moved = await Data(await c.PostAsJsonAsync($"{Vehicles}/{vehicle.GetProperty("id").GetGuid()}/transition", new { to = "inactive", reason = "Sold" }));
        Assert.Equal("inactive", moved.GetProperty("status").GetString());

        var wrongParent = await c.PostAsJsonAsync("/api/v1/m/logistics/records/fuel", new { legalEntityId = entity, title = "Fill", recordDate = "2026-01-06", parentId = Guid.NewGuid(), amount = 50m, data = new { litres = 20 } });
        Assert.Equal(HttpStatusCode.BadRequest, wrongParent.StatusCode);
    }

    [Fact]
    public async Task A_delivered_shipment_invoices_through_finance_and_voiding_releases_it()
    {
        var (c, entity, _) = await WorkspaceAsync("log-cargo");
        const string Shipments = "/api/v1/m/logistics/records/shipments";
        var customer = await PartyAsync(c, "Gulf Traders", "Customer");
        var shipment = await Data(await c.PostAsJsonAsync(Shipments, new
        {
            legalEntityId = entity, title = "20 pallets", recordDate = "2026-03-01", partyId = customer,
            data = new { origin = "Doha", destination = "Riyadh", mode = "Road" },
            lines = new[] { new { description = "Road freight", quantity = 1m, unitPrice = 1500m } },
        }), HttpStatusCode.Created);
        var id = shipment.GetProperty("id").GetGuid();
        Assert.Equal(1500m, shipment.GetProperty("amount").GetDecimal());

        var early = await c.PostAsJsonAsync($"{Shipments}/{id}/invoice", new { date = "2026-03-04" });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        foreach (var to in new[] { "picked", "intransit", "delivered" }) await Data(await c.PostAsJsonAsync($"{Shipments}/{id}/transition", new { to }));

        var invoiced = await Data(await c.PostAsJsonAsync($"{Shipments}/{id}/invoice", new { date = "2026-03-04" }));
        Assert.Equal("invoiced", invoiced.GetProperty("status").GetString());
        var invoiceId = invoiced.GetProperty("invoiceId").GetGuid();
        var invoice = await Data(await c.GetAsync($"/api/v1/finance/invoices/{invoiceId}"));
        Assert.Equal(1500m, invoice.GetProperty("total").GetDecimal());
        Assert.Equal("issued", invoice.GetProperty("status").GetString());
        Assert.True(invoice.GetProperty("journalEntryId").ValueKind == JsonValueKind.String);

        var twice = await c.PostAsJsonAsync($"{Shipments}/{id}/invoice", new { date = "2026-03-04" });
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        await Data(await c.PostAsJsonAsync($"/api/v1/finance/invoices/{invoiceId}/void", new { reason = "Wrong customer" }));
        var released = await Data(await c.GetAsync($"{Shipments}/{id}"));
        Assert.Equal(JsonValueKind.Null, released.GetProperty("invoiceId").ValueKind);
    }

    [Fact]
    public async Task An_approved_expense_claim_becomes_a_posted_bill_and_its_maker_cannot_approve_it()
    {
        var (c, entity, _) = await WorkspaceAsync("svc-consulting");
        const string Claims = "/api/v1/m/expenses/records/claims";
        var payee = await PartyAsync(c, "Amal Staff", "Vendor");
        var claim = await Data(await c.PostAsJsonAsync(Claims, new
        {
            legalEntityId = entity, title = "Client trip", recordDate = "2026-02-10", partyId = payee, data = new { category = "Travel" },
            lines = new[] { new { description = "Taxi", quantity = 2m, unitPrice = 40m } },
        }), HttpStatusCode.Created);
        var id = claim.GetProperty("id").GetGuid();
        await Data(await c.PostAsJsonAsync($"{Claims}/{id}/transition", new { to = "submitted" }));
        var self = await c.PostAsJsonAsync($"{Claims}/{id}/transition", new { to = "approved" });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);

        var second = await SecondAdminAsync(c);
        await Data(await second.PostAsJsonAsync($"{Claims}/{id}/transition", new { to = "approved" }));
        var billed = await Data(await second.PostAsJsonAsync($"{Claims}/{id}/bill", new { date = "2026-02-12" }));
        Assert.Equal("billed", billed.GetProperty("status").GetString());
        var bill = await Data(await second.GetAsync($"/api/v1/finance/bills/{billed.GetProperty("billId").GetGuid()}"));
        Assert.Equal(80m, bill.GetProperty("total").GetDecimal());
        Assert.Equal("issued", bill.GetProperty("status").GetString());
        var history = billed.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("toStatus").GetString()).ToList();
        Assert.Equal(["draft", "submitted", "approved", "billed"], history);
    }

    [Fact]
    public async Task Instalments_bill_one_at_a_time_and_material_issues_move_stock_and_cost()
    {
        var (c, entity, _) = await WorkspaceAsync("re-developer");
        const string Sales = "/api/v1/m/propertysales/records/salecontracts";
        var buyer = await PartyAsync(c, "Buyer One", "Customer");
        var contract = await Data(await c.PostAsJsonAsync(Sales, new
        {
            legalEntityId = entity, title = "Unit 12 sale", recordDate = "2026-01-15", partyId = buyer, data = new { unitRef = "A-12" },
            lines = new[]
            {
                new { description = "Down payment", quantity = 1m, unitPrice = 50000m, dueDate = "2026-01-20" },
                new { description = "Instalment 2", quantity = 1m, unitPrice = 25000m, dueDate = "2026-04-20" },
            },
        }), HttpStatusCode.Created);
        var id = contract.GetProperty("id").GetGuid();
        var approver = await SecondAdminAsync(c);
        await Data(await c.PostAsJsonAsync($"{Sales}/{id}/transition", new { to = "submitted" }));
        await Data(await approver.PostAsJsonAsync($"{Sales}/{id}/transition", new { to = "active" }));
        var firstLine = contract.GetProperty("lines")[0].GetProperty("id").GetGuid();
        var one = await Data(await c.PostAsJsonAsync($"{Sales}/{id}/invoice", new { date = "2026-01-20", lineIds = new[] { firstLine } }));
        Assert.Equal("active", one.GetProperty("status").GetString());
        var all = await Data(await c.PostAsJsonAsync($"{Sales}/{id}/invoice", new { date = "2026-04-20" }));
        Assert.Equal("completed", all.GetProperty("status").GetString());
        var nothing = await c.PostAsJsonAsync($"{Sales}/{id}/invoice", new { date = "2026-05-20" });
        Assert.Equal(HttpStatusCode.Conflict, nothing.StatusCode);

        var (b, buildEntity, _) = await WorkspaceAsync("con-contractor");
        var cement = (await Data(await b.PostAsJsonAsync("/api/v1/items", new { sku = "CEM-1", name = "Cement", type = "Product", uom = "bag", purchasePrice = 10m }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var warehouse = (await Data(await b.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new { code = "W1", name = "Yard", legalEntityId = buildEntity }), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        var equity = await SysAccountAsync(b, "equity.opening");
        var site = await Data(await b.PostAsJsonAsync("/api/v1/m/construction/records/sites", new { legalEntityId = buildEntity, title = "Tower", recordDate = "2026-01-01", data = new { location = "Doha" } }), HttpStatusCode.Created);
        var issue = await Data(await b.PostAsJsonAsync("/api/v1/m/construction/records/materialissues", new
        {
            legalEntityId = buildEntity, title = "Foundations", recordDate = "2026-02-01", parentId = site.GetProperty("id").GetGuid(),
            lines = new[] { new { description = "Cement", quantity = 5m, unitPrice = 10m, itemId = cement } },
        }), HttpStatusCode.Created);
        var issueId = issue.GetProperty("id").GetGuid();
        var empty = await b.PostAsJsonAsync($"/api/v1/m/construction/records/materialissues/{issueId}/consume", new { date = "2026-02-01", warehouseId = warehouse });
        Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);

        await Data(await b.PostAsJsonAsync("/api/v1/m/inventory/stock/receive", new { warehouseId = warehouse, itemId = cement, quantity = 20m, unitCost = 10m, date = "2026-01-15", offsetAccountId = equity }), HttpStatusCode.Created);
        var consumed = await Data(await b.PostAsJsonAsync($"/api/v1/m/construction/records/materialissues/{issueId}/consume", new { date = "2026-02-01", warehouseId = warehouse }));
        Assert.Equal("2026-02-01", consumed.GetProperty("consumedOn").GetString());
        var stock = await Data(await b.GetAsync("/api/v1/m/inventory/stock"));
        var rows = stock.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(15m, rows.Single().GetProperty("quantity").GetDecimal());

        await Data(await b.PostAsJsonAsync($"/api/v1/m/construction/records/materialissues/{issueId}/transition", new { to = "issued" }));
        await Data(await b.PostAsJsonAsync($"/api/v1/m/construction/records/materialissues/{issueId}/transition", new { to = "cancelled", reason = "Returned to yard" }));
        var after = await Data(await b.GetAsync("/api/v1/m/inventory/stock"));
        Assert.Equal(20m, after.GetProperty("rows").EnumerateArray().Single().GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Patient_details_need_the_restricted_permission_and_opening_a_patient_is_traced()
    {
        var (c, entity, _) = await WorkspaceAsync("health-clinic");
        const string Patients = "/api/v1/m/healthcare/records/patients";
        var patient = await Data(await c.PostAsJsonAsync(Patients, new { legalEntityId = entity, title = "Test Patient", recordDate = "2026-01-01", data = new { bloodGroup = "O+", insurer = "Acme Mutual" } }), HttpStatusCode.Created);
        var id = patient.GetProperty("id").GetGuid();
        Assert.Equal("O+", patient.GetProperty("data").GetProperty("bloodGroup").GetString());

        var roles = await Data(await c.GetAsync("/api/v1/roles"));
        var viewer = (roles.ValueKind == JsonValueKind.Array ? roles : roles.GetProperty("items")).EnumerateArray().First(r => r.GetProperty("name").GetString() == "Healthcare administration viewer").GetProperty("id").GetGuid();
        var limited = await UserAsync(c, viewer);

        var seen = await Data(await limited.GetAsync($"{Patients}/{id}"));
        Assert.False(seen.GetProperty("data").TryGetProperty("bloodGroup", out _));
        Assert.Equal("Acme Mutual", seen.GetProperty("data").GetProperty("insurer").GetString());
        var list = await Data(await limited.GetAsync(Patients));
        Assert.False(list[0].GetProperty("data").TryGetProperty("bloodGroup", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.PostAsJsonAsync(Patients, new { legalEntityId = entity, title = "X", recordDate = "2026-01-01" })).StatusCode);

        var trail = await Data(await c.GetAsync($"/api/v1/audit/record?recordType=healthcare.patients&recordId={id}"));
        var items = trail.ValueKind == JsonValueKind.Array ? trail : trail.GetProperty("items");
        Assert.Contains(items.EnumerateArray(), e => e.GetProperty("action").GetString() == "Viewed");

        // Another workspace cannot reach the record at all.
        var (other, _, _) = await WorkspaceAsync("health-clinic");
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Patients}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Capabilities_hide_menu_items_and_close_their_api_while_keeping_data()
    {
        var (c, entity, _) = await WorkspaceAsync("svc-consulting");
        var profile = await Data(await c.GetAsync("/api/v1/setup/profile"));
        Assert.False(profile.GetProperty("capabilities").EnumerateArray().First(x => x.GetProperty("key").GetString() == "cheques").GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/finance/cheques")).StatusCode);

        await Data(await c.PutAsJsonAsync("/api/v1/setup/capabilities", new { disabled = new[] { "budgets" } }));
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/finance/budgets")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/v1/finance/cheques")).StatusCode);
        var items = (await Data(await c.GetAsync("/api/v1/navigation/menu"))).EnumerateArray().SelectMany(g => g.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("id").GetString()).ToList();
        Assert.DoesNotContain("finance.budgets", items);
        Assert.Contains("finance.cheques", items);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/v1/setup/capabilities", new { disabled = new[] { "nonsense" } })).StatusCode);
    }

    [Fact]
    public async Task Changing_business_type_only_adds_and_numbering_can_be_changed()
    {
        var (c, entity, _) = await WorkspaceAsync("generic");
        var changed = await Data(await c.PostAsJsonAsync("/api/v1/setup/industry", new { industry = "con-contractor" }));
        var enabled = changed.GetProperty("modulesEnabled").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("construction", enabled);
        Assert.True(enabled.IndexOf("inventory") < enabled.IndexOf("construction"), "A module's requirements come first.");
        Assert.Contains("Construction manager", changed.GetProperty("rolesAdded").EnumerateArray().Select(r => r.GetString()));

        // Move again: construction stays on, nothing is taken away.
        await Data(await c.PostAsJsonAsync("/api/v1/setup/industry", new { industry = "edu-school" }));
        var modules = (await Data(await c.GetAsync("/api/v1/modules"))).EnumerateArray().ToDictionary(m => m.GetProperty("key").GetString()!, m => m.GetProperty("state").GetString());
        Assert.Equal("enabled", modules["construction"]);
        Assert.Equal("enabled", modules["education"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/setup/industry", new { industry = "nope" })).StatusCode);

        var bad = await c.PutAsJsonAsync("/api/v1/setup/sequences/invoice", new { prefix = "BILL", pattern = "{prefix}-{nope}", resetYearly = false });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var seq = await Data(await c.PutAsJsonAsync("/api/v1/setup/sequences/invoice", new { prefix = "TAX", pattern = "{prefix}/{yy}/{seq:4}", resetYearly = true }));
        Assert.StartsWith("TAX/", seq.GetProperty("example").GetString());
        var party = await PartyAsync(c, "Numbered", "Customer");
        var invoice = await Data(await c.PostAsJsonAsync("/api/v1/finance/invoices", new { legalEntityId = entity, partyId = party, invoiceDate = "2026-03-01", lines = new[] { new { description = "Work", quantity = 1m, price = 100m } } }), HttpStatusCode.Created);
        var issued = await Data(await c.PostAsJsonAsync($"/api/v1/finance/invoices/{invoice.GetProperty("id").GetGuid()}/issue", new { }));
        Assert.StartsWith("TAX/26/", issued.GetProperty("number").GetString());
    }

    [Fact]
    public async Task Enabling_a_module_later_brings_the_accounts_and_roles_it_needs()
    {
        var (c, _, _) = await WorkspaceAsync("generic");
        await Data(await c.PostAsync("/api/v1/modules/realestate/enable", null));
        Assert.NotEqual(Guid.Empty, await SysAccountAsync(c, "realestate.income.leasing"));
        await Data(await c.PostAsync("/api/v1/modules/inventory/enable", null));
        await Data(await c.PostAsync("/api/v1/modules/projects/enable", null));
        await Data(await c.PostAsync("/api/v1/modules/construction/enable", null));
        var roles = await Data(await c.GetAsync("/api/v1/roles"));
        Assert.Contains((roles.ValueKind == JsonValueKind.Array ? roles : roles.GetProperty("items")).EnumerateArray(), r => r.GetProperty("name").GetString() == "Construction manager");

        // A module that needs another one says so.
        var (d, _, _) = await WorkspaceAsync("generic");
        Assert.Equal(HttpStatusCode.Conflict, (await d.PostAsync("/api/v1/modules/construction/enable", null)).StatusCode);
    }

    [Fact]
    public async Task The_owner_dashboard_comes_from_the_ledger_and_splits_by_branch()
    {
        var (c, entity, _) = await WorkspaceAsync("svc-consulting");
        var customer = await PartyAsync(c, "Client A", "Customer");
        var unit = await Data(await c.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = entity, code = "NORTH", name = "North", kind = "branch" }), HttpStatusCode.Created);
        var invoice = await Data(await c.PostAsJsonAsync("/api/v1/finance/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = "2026-03-01", businessUnitId = unit.GetProperty("id").GetGuid(), lines = new[] { new { description = "Work", quantity = 1m, price = 1000m } } }), HttpStatusCode.Created);
        await Data(await c.PostAsJsonAsync($"/api/v1/finance/invoices/{invoice.GetProperty("id").GetGuid()}/issue", new { }));

        var owner = await Data(await c.GetAsync("/api/v1/dashboard/owner?from=2026-01-01&to=2026-06-30"));
        var sections = owner.GetProperty("sections").EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!);
        decimal Card(string section, string key) => sections[section].GetProperty("cards").EnumerateArray().First(x => x.GetProperty("key").GetString() == key).GetProperty("value").GetDecimal();
        Assert.Equal(1000m, Card("revenue", "revenue"));
        Assert.Equal(1000m, Card("gross-profit", "gross-profit"));
        Assert.Equal(100m, Card("gross-profit", "gross-margin"));
        Assert.Equal(1000m, Card("receivables", "receivables"));
        Assert.Equal("North", sections["branch"].GetProperty("table").GetProperty("rows")[0][0].GetString());

        // The business type's focus comes first.
        Assert.Equal("revenue", owner.GetProperty("sections")[0].GetProperty("key").GetString());
        var outOfPeriod = await Data(await c.GetAsync("/api/v1/dashboard/owner?from=2026-04-01&to=2026-06-30"));
        Assert.Equal(0m, outOfPeriod.GetProperty("sections").EnumerateArray().First(s => s.GetProperty("key").GetString() == "revenue").GetProperty("cards")[0].GetProperty("value").GetDecimal());
    }
}
