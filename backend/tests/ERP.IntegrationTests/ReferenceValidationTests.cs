using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Commands refuse references to rows of another workspace: most foreign keys ignore the workspace, so the database would accept them.</summary>
public class ReferenceValidationTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Sales = "/api/v1/m/sales", Pur = "/api/v1/m/purchasing", Svc = "/api/v1/m/services", Retail = "/api/v1/m/retail", Dist = "/api/v1/m/distribution", Inv = "/api/v1/m/inventory";

    private sealed record Setup(HttpClient C, string Entity, string OtherEntity, string Party, string Item, string Warehouse, string ForeignParty, string ForeignItem, string ForeignUnit, string ForeignQuote);

    private static async Task<string> PartyAsync(HttpClient c) => (string)(await HrKit.Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = HrKit.Unique("Party"), kind = "Company", roles = "Customer,Vendor" }), HttpStatusCode.Created))["id"]!;
    private static async Task<string> ItemAsync(HttpClient c) => (string)(await HrKit.Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku = HrKit.Unique("SKU"), name = "Thing", type = "product", uom = "EA", isActive = true }), HttpStatusCode.Created))["id"]!;
    private static async Task<string> EntityAsync(HttpClient c) => (string)(await HrKit.Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = HrKit.Unique("Refs"), countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    private static object QuoteBody(string entity, string? party, string? item) => new
    {
        legalEntityId = entity, customerPartyId = party, customerName = "Buyer", quoteDate = "2026-06-01", validUntil = "2026-06-30", currency = "AED",
        lines = new[] { new { itemId = item, itemCode = "X", description = "Line", quantity = 1m, unitPrice = 10m, discountPercent = 0m, taxRate = 0m } },
    };

    private static object OrderBody(string entity, string? party, string? item, string? quote = null) => new
    {
        legalEntityId = entity, quoteId = quote, customerPartyId = party, customerName = "Buyer", orderDate = "2026-06-01", currency = "AED",
        lines = new[] { new { itemId = item, itemCode = "X", description = "Line", quantity = 1m, unitPrice = 10m, discountPercent = 0m, taxRate = 0m } },
    };

    private static object PurchaseBody(string entity, string? item) => new
    {
        legalEntityId = entity, vendorName = "Seller", orderDate = "2026-06-01", currency = "AED",
        lines = new[] { new { itemId = item, itemCode = "X", description = "Line", quantity = 1m, unitPrice = 10m } },
    };

    private static object StopBody(string party) => new { customerPartyId = party, customerName = "Stop", sequenceOrder = 1 };

    private async Task<Setup> StartAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        foreach (var m in new[] { "sales", "purchasing", "services", "retail", "distribution", "inventory" }) await first.PostAsync($"/api/v1/modules/{m}/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = await EntityAsync(c);
        var other = await EntityAsync(c);
        var warehouse = (string)(await HrKit.Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = HrKit.Unique("W"), name = "Here", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;

        var (foreign0, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await foreign0.PostAsync("/api/v1/modules/sales/enable", null);
        var (foreign, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var foreignEntity = await EntityAsync(foreign);
        var foreignParty = await PartyAsync(foreign);
        var foreignItem = await ItemAsync(foreign);
        var foreignUnit = (string)(await HrKit.Ok(await foreign.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = foreignEntity, code = HrKit.Unique("BR"), name = "Their branch" }), HttpStatusCode.Created))["id"]!;
        var foreignQuote = (string)(await HrKit.Ok(await foreign.PostAsJsonAsync($"{Sales}/quotes", QuoteBody(foreignEntity, foreignParty, foreignItem)), HttpStatusCode.Created))["id"]!;

        return new Setup(c, entity, other, await PartyAsync(c), await ItemAsync(c), warehouse, foreignParty, foreignItem, foreignUnit, foreignQuote);
    }

    private static async Task RefusedAsync(HttpResponseMessage res, string field)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest && text.Contains(field, StringComparison.OrdinalIgnoreCase), $"Expected 400 on {field} but got {res.StatusCode}: {text}");
    }

    [Fact]
    public async Task Sales_quotes_check_the_customer_and_items_on_create_and_update()
    {
        var s = await StartAsync();
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Sales}/quotes", QuoteBody(s.Entity, s.ForeignParty, s.Item)), "customerPartyId");
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Sales}/quotes", QuoteBody(s.Entity, s.Party, s.ForeignItem)), "lines");

        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Sales}/quotes", QuoteBody(s.Entity, s.Party, s.Item)), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Sales}/quotes/{id}", QuoteBody(s.Entity, s.ForeignParty, s.Item)), "customerPartyId");
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Sales}/quotes/{id}", QuoteBody(s.Entity, s.Party, s.ForeignItem)), "lines");
        await HrKit.Ok(await s.C.PutAsJsonAsync($"{Sales}/quotes/{id}", QuoteBody(s.Entity, null, null))); // a walk-in customer and a free-text line stay allowed
    }

    [Fact]
    public async Task Sales_orders_check_the_customer_items_and_quote_on_create_and_update()
    {
        var s = await StartAsync();
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Sales}/orders", OrderBody(s.Entity, s.ForeignParty, s.Item)), "customerPartyId");
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Sales}/orders", OrderBody(s.Entity, s.Party, s.ForeignItem)), "lines");
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Sales}/orders", OrderBody(s.Entity, s.Party, s.Item, s.ForeignQuote)), "quoteId");

        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Sales}/orders", OrderBody(s.Entity, s.Party, s.Item)), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Sales}/orders/{id}", OrderBody(s.Entity, s.ForeignParty, s.Item)), "customerPartyId");
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Sales}/orders/{id}", OrderBody(s.Entity, s.Party, s.ForeignItem)), "lines");
    }

    [Fact]
    public async Task Purchase_orders_check_their_line_items_on_create_and_update()
    {
        var s = await StartAsync();
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Pur}/orders", PurchaseBody(s.Entity, s.ForeignItem)), "lines");
        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Pur}/orders", PurchaseBody(s.Entity, s.Item)), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Pur}/orders/{id}", PurchaseBody(s.Entity, s.ForeignItem)), "lines");
    }

    [Fact]
    public async Task Manual_journals_check_the_party_and_business_unit_of_each_line_on_create_and_update()
    {
        var s = await StartAsync();
        var accounts = (await HrKit.Ok(await s.C.GetAsync("/api/v1/finance/accounts?pageSize=200"))).AsArray().Where(a => (bool?)a!["isPostable"] == true).Select(a => (string)a!["id"]!).Take(2).ToList();
        object Journal(string? party, string? unit) => new
        {
            legalEntityId = s.Entity, date = "2026-03-01",
            lines = new object[]
            {
                new { accountId = accounts[0], debit = 10m, credit = 0m, partyId = party, businessUnitId = unit },
                new { accountId = accounts[1], debit = 0m, credit = 10m },
            },
        };

        await RefusedAsync(await s.C.PostAsJsonAsync("/api/v1/finance/journals", Journal(s.ForeignParty, null)), "lines");
        await RefusedAsync(await s.C.PostAsJsonAsync("/api/v1/finance/journals", Journal(null, s.ForeignUnit)), "lines");
        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync("/api/v1/finance/journals", Journal(s.Party, null)), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"/api/v1/finance/journals/{id}", Journal(s.ForeignParty, null)), "lines");
    }

    [Fact]
    public async Task Service_engagements_check_the_client_and_lead_consultant()
    {
        var s = await StartAsync();
        object Body(string client, Guid? lead = null) => new { legalEntityId = s.Entity, title = HrKit.Unique("Eng"), clientId = client, clientName = "Client", type = 1, startDate = "2026-05-01", leadConsultantUserId = lead };
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Svc}/engagements", Body(s.ForeignParty)), "clientId");
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Svc}/engagements", Body(s.Party, Guid.NewGuid())), "leadConsultantUserId");
        await HrKit.Ok(await s.C.PostAsJsonAsync($"{Svc}/engagements", Body(s.Party)), HttpStatusCode.Created);
    }

    [Fact]
    public async Task Retail_stores_check_the_warehouse_on_create_and_update()
    {
        var s = await StartAsync();
        object Create(string? warehouse) => new { legalEntityId = s.Entity, code = HrKit.Unique("S"), name = "Store", warehouseId = warehouse, isActive = true };
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Retail}/stores", Create(Guid.NewGuid().ToString())), "warehouseId");
        // a warehouse of another company in this workspace is refused too
        var otherWarehouse = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Inv}/warehouses", new { code = HrKit.Unique("W"), name = "There", legalEntityId = s.OtherEntity }), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Retail}/stores", Create(otherWarehouse)), "warehouseId");

        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Retail}/stores", Create(s.Warehouse)), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Retail}/stores/{id}", new { id, name = "Store", warehouseId = otherWarehouse, isActive = true }), "warehouseId");
        await HrKit.Ok(await s.C.PutAsJsonAsync($"{Retail}/stores/{id}", new { id, name = "Store 2", warehouseId = s.Warehouse, isActive = true }));
    }

    [Fact]
    public async Task Distribution_routes_check_the_warehouse_and_each_stops_customer_on_create_and_update()
    {
        var s = await StartAsync();
        object Create(string? warehouse, params object[] stops) => new { legalEntityId = s.Entity, code = HrKit.Unique("R"), name = "Route", warehouseId = warehouse, isActive = true, stops };
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Dist}/routes", Create(Guid.NewGuid().ToString())), "warehouseId");
        await RefusedAsync(await s.C.PostAsJsonAsync($"{Dist}/routes", Create(s.Warehouse, StopBody(s.ForeignParty))), "stops");

        var id = (string)(await HrKit.Ok(await s.C.PostAsJsonAsync($"{Dist}/routes", Create(s.Warehouse, StopBody(s.Party))), HttpStatusCode.Created))["id"]!;
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Dist}/routes/{id}", new { id, name = "Route", isActive = true, warehouseId = s.Warehouse, stops = new[] { StopBody(s.ForeignParty) } }), "stops");
        await RefusedAsync(await s.C.PutAsJsonAsync($"{Dist}/routes/{id}", new { id, name = "Route", isActive = true, warehouseId = Guid.NewGuid() }), "warehouseId");
    }
}
