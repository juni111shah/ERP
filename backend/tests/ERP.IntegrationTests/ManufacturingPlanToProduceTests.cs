using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Security;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>Material through WIP to finished goods: every test reads the ledger and the stock figures, not just the status.</summary>
public class ManufacturingPlanToProduceTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Inv = "/api/v1/m/inventory";
    private const string Mfg = "/api/v1/m/manufacturing";
    private const string Inventory = "1300", Wip = "1350", Cogs = "5000", GeneralExpense = "6900";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Warehouse, string Raw, string Finished, string BomId, string PayableAccount);

    private static string Today => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private static async Task<string> ItemAsync(HttpClient c, string prefix, decimal? purchasePrice = null)
        => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku = prefix + "-" + Guid.NewGuid().ToString("N")[..6], name = prefix + " item", type = "product", uom = "pcs", purchasePrice, isActive = true }), HttpStatusCode.Created))["id"]!;

    /// <summary>20 of the raw material at 50 in stock (1000), and a BOM of 2 raw plus 15 conversion for one finished unit.</summary>
    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/manufacturing/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Plant " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = "WH-" + Guid.NewGuid().ToString("N")[..4], name = "Plant store", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var raw = await ItemAsync(c, "RAW", 50m);
        var finished = await ItemAsync(c, "FG");

        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        var payable = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;
        await ReceiveAsync(c, wh, raw, 20, 50m, payable);

        var bom = await Ok(await c.PostAsJsonAsync($"{Mfg}/boms", BomBody(entity, finished, labour: 10m, overhead: 5m, components: [Component(raw, 2m, cost: 50m)])), HttpStatusCode.Created);
        return new Ctx(c, entity, wh, raw, finished, (string)bom["id"]!, payable);
    }

    private static async Task ReceiveAsync(HttpClient c, string wh, string item, decimal quantity, decimal cost, string offset)
        => await Ok(await c.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = wh, itemId = item, quantity, unitCost = cost, date = Today, offsetAccountId = offset }), HttpStatusCode.Created);

    private static object BomBody(string entity, string produced, decimal labour = 0m, decimal overhead = 0m, object[]? components = null, bool isActive = true, decimal output = 1m) => new
    {
        legalEntityId = entity, code = "B-" + Guid.NewGuid().ToString("N")[..8], name = "Test BOM", producedItemId = produced, producedItemCode = "X", producedItemName = "X",
        outputQuantity = output, uom = "pcs", version = "1.0", laborCostPerUnit = labour, overheadCostPerUnit = overhead, isActive, components = components ?? [],
    };

    private static object Component(string item, decimal qty, decimal? cost = null, decimal scrap = 0m)
        => new { componentItemId = item, componentItemCode = "X", componentItemName = "X", quantityRequired = qty, uom = "pcs", scrapPercentage = scrap, unitCost = cost };

    private static async Task<JsonNode> NewOrderAsync(Ctx x, decimal planned = 1m, object[]? operations = null, string? bomId = null)
        => await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders", new { legalEntityId = x.Entity, bomId = bomId ?? x.BomId, plannedQuantity = planned, warehouseId = x.Warehouse, startDate = Today, dueDate = Today, operations }), HttpStatusCode.Created);

    private static async Task<string> ReleasedOrderAsync(Ctx x, decimal planned = 1m, object[]? operations = null)
    {
        var id = (string)(await NewOrderAsync(x, planned, operations))["id"]!;
        await Ok(await x.C.PostAsync($"{Mfg}/workorders/{id}/release", null));
        return id;
    }

    private static Task<HttpResponseMessage> CompleteAsync(Ctx x, string id, decimal made, decimal scrapped = 0m, string? date = null)
        => x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/complete", new { completedQuantity = made, scrappedQuantity = scrapped, actualTotalCost = 0, completionDate = date });

    private static async Task<Dictionary<string, decimal>> LedgerAsync(Ctx x)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        Assert.True((bool)tb["balanced"]!, "the trial balance must balance");
        return tb["rows"]!.AsArray().ToDictionary(r => (string)r!["code"]!, r => (decimal)r!["debit"]! - (decimal)r["credit"]!);
    }

    private static decimal Delta(Dictionary<string, decimal> before, Dictionary<string, decimal> after, string code) => after.GetValueOrDefault(code) - before.GetValueOrDefault(code);

    private static async Task<decimal> StockAsync(Ctx x, string item)
    {
        var rows = (await Ok(await x.C.GetAsync($"{Inv}/stock?warehouseId={x.Warehouse}&itemId={item}")))["rows"]!.AsArray();
        return rows.Sum(r => (decimal)r!["quantity"]!);
    }

    private static async Task SettingAsync(Ctx x, string key, object value)
        => Assert.Equal(HttpStatusCode.NoContent, (await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { [key] = value })).StatusCode);

    [Fact]
    public async Task Issuing_moves_stock_into_wip_and_completion_clears_wip_into_finished_goods()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x);
        var start = await LedgerAsync(x);

        // Actual consumption differs from the BOM's 2.
        var issued = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/issue", new { lines = new[] { new { itemId = x.Raw, quantity = 3m } } }));
        Assert.Equal(150m, (decimal)issued["materialIssues"]![0]!["value"]!);
        var afterIssue = await LedgerAsync(x);
        Assert.Equal(150m, Delta(start, afterIssue, Wip));
        Assert.Equal(-150m, Delta(start, afterIssue, Inventory));
        Assert.Equal(17m, await StockAsync(x, x.Raw));

        var done = await Ok(await CompleteAsync(x, id, 1m));
        Assert.Equal(165m, (decimal)done["actualTotalCost"]!);
        Assert.Equal(150m, (decimal)done["materialCost"]!);
        Assert.Equal(15m, (decimal)done["conversionCost"]!);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.Parse((string)done["completionDate"]!));

        var end = await LedgerAsync(x);
        Assert.Equal(0m, Delta(start, end, Wip));                       // WIP clears
        Assert.Equal(15m, Delta(start, end, Inventory));                // -150 material, +165 finished goods
        Assert.Equal(-15m, Delta(start, end, GeneralExpense));          // conversion credited to absorption (falls back here without its own account)
        Assert.Equal(0m, Delta(start, end, Cogs));
        Assert.Equal(17m, await StockAsync(x, x.Raw));                  // nothing more backflushed: 3 is already past the BOM's 2
        Assert.Equal(1m, await StockAsync(x, x.Finished));
    }

    [Fact]
    public async Task Material_can_be_issued_on_release_and_unused_material_returned_at_its_issue_cost()
    {
        var x = await SetupAsync();
        var id = (string)(await NewOrderAsync(x, 5m))["id"]!;
        var start = await LedgerAsync(x);

        var released = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/release", new { issueMaterials = true }));
        Assert.Equal("InProgress", (string)released["status"]!, ignoreCase: true);
        Assert.Equal(10m, (decimal)released["materialIssues"]![0]!["quantity"]!);
        Assert.Equal(10m, await StockAsync(x, x.Raw));
        Assert.Equal(500m, Delta(start, await LedgerAsync(x), Wip));

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/return-materials", new { lines = new[] { new { itemId = x.Raw, quantity = 11m } } })).StatusCode);
        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/return-materials", new { lines = new[] { new { itemId = x.Raw, quantity = 2m } } }));
        Assert.Equal(12m, await StockAsync(x, x.Raw));
        Assert.Equal(400m, Delta(start, await LedgerAsync(x), Wip));

        // Backflush at completion tops the order up to what the BOM says the output took.
        var done = await Ok(await CompleteAsync(x, id, 5m));
        Assert.Equal(575m, (decimal)done["actualTotalCost"]!); // 10 x 50 + 5 x 15
        Assert.Equal(10m, await StockAsync(x, x.Raw));
        Assert.Equal(0m, Delta(start, await LedgerAsync(x), Wip));
    }

    [Fact]
    public async Task Stock_is_guarded_on_issue_and_on_backflush_unless_the_setting_allows_negative()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x, 15m); // needs 30 of the 20 on hand
        var start = await LedgerAsync(x);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/issue", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/issue", new { lines = new[] { new { itemId = x.Raw, quantity = 21m } } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CompleteAsync(x, id, 15m)).StatusCode);

        Assert.Equal(20m, await StockAsync(x, x.Raw));
        Assert.Equal("InProgress", (string)(await Ok(await x.C.GetAsync($"{Mfg}/workorders/{id}")))["status"]!, ignoreCase: true);
        Assert.Equal(0m, Delta(start, await LedgerAsync(x), Inventory));

        await SettingAsync(x, "manufacturing.allowNegativeStock", true);
        try
        {
            var done = await Ok(await CompleteAsync(x, id, 15m));
            Assert.Equal(30m, (decimal)done["materialIssues"]![0]!["quantity"]!); // the stock report floors at zero, so read what moved
            Assert.Equal(1500m, (decimal)done["materialCost"]!);
        }
        finally { await SettingAsync(x, "manufacturing.allowNegativeStock", false); }
    }

    [Fact]
    public async Task Turning_backflush_off_consumes_only_what_was_issued_and_wip_still_clears()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x);
        var start = await LedgerAsync(x);

        await SettingAsync(x, "manufacturing.autoConsumeComponents", false);
        try
        {
            var done = await Ok(await CompleteAsync(x, id, 1m));
            Assert.Equal(15m, (decimal)done["actualTotalCost"]!);
            Assert.Equal(0m, (decimal)done["materialCost"]!);
        }
        finally { await SettingAsync(x, "manufacturing.autoConsumeComponents", true); }

        Assert.Equal(20m, await StockAsync(x, x.Raw));
        var end = await LedgerAsync(x);
        Assert.Equal(0m, Delta(start, end, Wip));
        Assert.Equal(15m, Delta(start, end, Inventory));
    }

    [Fact]
    public async Task Scrapped_units_are_costed_to_cost_of_sales_and_do_not_reach_stock()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x, 2m);
        var start = await LedgerAsync(x);

        var done = await Ok(await CompleteAsync(x, id, 1m, 1m));
        Assert.Equal(230m, (decimal)done["actualTotalCost"]!); // 4 x 50 + 2 x 15
        Assert.Equal(115m, (decimal)done["scrapCost"]!);

        var end = await LedgerAsync(x);
        Assert.Equal(115m, Delta(start, end, Cogs));
        Assert.Equal(-85m, Delta(start, end, Inventory));      // -200 material, +115 finished
        Assert.Equal(-30m, Delta(start, end, GeneralExpense));
        Assert.Equal(0m, Delta(start, end, Wip));
        Assert.Equal(1m, await StockAsync(x, x.Finished));
        Assert.Equal(16m, await StockAsync(x, x.Raw));
    }

    [Fact]
    public async Task Completed_quantity_and_date_are_bounded_and_the_bom_scrap_allowance_is_issued()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x, 2m);

        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(x, id, 3m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(x, id, 2m, 1m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(x, id, 0m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(x, id, 1m, -1m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CompleteAsync(x, id, 1m, date: DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"))).StatusCode);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var done = await Ok(await CompleteAsync(x, id, 2m, date: yesterday.ToString("yyyy-MM-dd")));
        Assert.Equal(yesterday, DateOnly.Parse((string)done["completionDate"]!));

        var wasteful = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, x.Finished, components: [Component(x.Raw, 2m, cost: 50m, scrap: 10m)])), HttpStatusCode.Created);
        var order = (string)(await NewOrderAsync(x, 1m, bomId: (string)wasteful["id"]!))["id"]!;
        await Ok(await x.C.PostAsync($"{Mfg}/workorders/{order}/release", null));
        var before = await StockAsync(x, x.Raw);
        await Ok(await CompleteAsync(x, order, 1m));
        Assert.Equal(before - 2.2m, await StockAsync(x, x.Raw));
    }

    [Fact]
    public async Task Actual_hours_replace_planned_hours_in_the_conversion_cost()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x, 1m, [new { sequenceOrder = 1, operationName = "Cut", workCenterName = "Bench", plannedHours = 2m, hourlyRate = 20m }]);
        var operation = (string)(await Ok(await x.C.GetAsync($"{Mfg}/workorders/{id}")))["operations"]![0]!["id"]!;
        var start = await LedgerAsync(x);

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Mfg}/workorders/{id}/operations/{operation}/hours", new { actualHours = -1m })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.C.PutAsJsonAsync($"{Mfg}/workorders/{id}/operations/{Guid.NewGuid()}/hours", new { actualHours = 1m })).StatusCode);
        var recorded = await Ok(await x.C.PutAsJsonAsync($"{Mfg}/workorders/{id}/operations/{operation}/hours", new { actualHours = 3m }));
        Assert.Equal(3m, (decimal)recorded["operations"]![0]!["actualHours"]!);

        var done = await Ok(await CompleteAsync(x, id, 1m));
        Assert.Equal(60m, (decimal)done["conversionCost"]!); // 3 x 20, not the 2 planned nor the BOM's 15
        Assert.Equal(-60m, Delta(start, await LedgerAsync(x), GeneralExpense));
    }

    [Fact]
    public async Task Reversing_a_completed_order_restores_stock_and_ledger_and_leaves_a_trail()
    {
        var x = await SetupAsync();
        var start = await LedgerAsync(x);
        var id = await ReleasedOrderAsync(x, 2m);
        await Ok(await CompleteAsync(x, id, 1m, 1m));
        Assert.Equal(16m, await StockAsync(x, x.Raw));

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/reverse", new { reason = "  " })).StatusCode);

        var reversed = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/reverse", new { reason = "Wrong batch booked" }));
        Assert.Equal("Reversed", (string)reversed["status"]!, ignoreCase: true);
        Assert.Equal("Wrong batch booked", (string)reversed["reversalReason"]!);
        Assert.NotNull(reversed["reversedAt"]);

        Assert.Equal(20m, await StockAsync(x, x.Raw));
        Assert.Equal(0m, await StockAsync(x, x.Finished));
        var end = await LedgerAsync(x);
        foreach (var code in new[] { Inventory, Wip, Cogs, GeneralExpense }) Assert.Equal(0m, Delta(start, end, code));

        var events = (await Ok(await x.C.GetAsync($"/api/v1/audit?filter=recordType:eq:WorkOrder&filter=recordId:eq:{id}"))).AsArray();
        var trail = events.First(e => (string?)e!["action"] == "Reversed")!;
        Assert.Contains("Wrong batch booked", (string)(await Ok(await x.C.GetAsync($"/api/v1/audit/{(string)trail["id"]!}")))["after"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/reverse", new { reason = "Again" })).StatusCode);
    }

    [Fact]
    public async Task A_completed_order_cannot_be_reversed_once_its_output_has_left_stock_or_without_the_permission()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x);
        await Ok(await CompleteAsync(x, id, 1m));

        var role = await Ok(await x.C.PostAsJsonAsync("/api/v1/roles", new RoleInput("Role_" + Guid.NewGuid().ToString("N")[..8], "Planner", [ERP.Modules.Manufacturing.Permissions.WorkOrdersManage, ERP.Modules.Manufacturing.Permissions.WorkOrdersRead])), HttpStatusCode.Created);
        var email = $"planner_{Guid.NewGuid():N}@demo.local";
        (await x.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Planner", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { Guid.Parse((string)role["id"]!) } })).EnsureSuccessStatusCode();
        var (planner, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        Assert.Equal(HttpStatusCode.Forbidden, (await planner.PostAsJsonAsync($"{Mfg}/workorders/{id}/reverse", new { reason = "Not mine to do" })).StatusCode);

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Warehouse, itemId = x.Finished, quantity = 1m, date = Today, expenseAccountId = (string?)null }), HttpStatusCode.Created);
        var before = await LedgerAsync(x);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/reverse", new { reason = "Too late" })).StatusCode);
        Assert.Equal("Completed", (string)(await Ok(await x.C.GetAsync($"{Mfg}/workorders/{id}")))["status"]!, ignoreCase: true);
        Assert.Equal(18m, await StockAsync(x, x.Raw));
        Assert.Equal(0m, Delta(before, await LedgerAsync(x), Inventory));
    }

    [Fact]
    public async Task Cancelling_after_an_issue_puts_the_material_back_and_clears_wip()
    {
        var x = await SetupAsync();
        var start = await LedgerAsync(x);
        var id = await ReleasedOrderAsync(x);
        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/issue", new { }));
        Assert.Equal(18m, await StockAsync(x, x.Raw));

        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{id}/cancel", new { reason = "Order withdrawn" }));

        Assert.Equal(20m, await StockAsync(x, x.Raw));
        var end = await LedgerAsync(x);
        Assert.Equal(0m, Delta(start, end, Wip));
        Assert.Equal(0m, Delta(start, end, Inventory));
    }

    [Fact]
    public async Task With_inventory_off_completion_posts_no_phantom_stock()
    {
        var x = await SetupAsync();
        var id = await ReleasedOrderAsync(x);
        var start = await LedgerAsync(x);

        Assert.Equal(HttpStatusCode.OK, (await x.C.PostAsync("/api/v1/modules/inventory/disable", null)).StatusCode);
        try
        {
            var (off, _) = await f.SignInAsync(ApiFactory.AdminEmail);
            var done = await Ok(await off.PostAsJsonAsync($"{Mfg}/workorders/{id}/complete", new { completedQuantity = 1m, scrappedQuantity = 0m, actualTotalCost = 0 }));
            Assert.Equal("Completed", (string)done["status"]!, ignoreCase: true);
            Assert.Equal(0m, (decimal)done["materialCost"]!);
            Assert.Empty(done["materialIssues"]!.AsArray());

            var end = await LedgerAsync(x with { C = off });
            foreach (var code in new[] { Inventory, Wip, Cogs, GeneralExpense }) Assert.Equal(0m, Delta(start, end, code));
        }
        finally { (await x.C.PostAsync("/api/v1/modules/inventory/enable", null)).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task A_bom_line_with_no_typed_cost_follows_the_current_average()
    {
        var x = await SetupAsync();
        var part = await ItemAsync(x.C, "PART", 7m);
        var product = await ItemAsync(x.C, "PROD");
        await ReceiveAsync(x.C, x.Warehouse, part, 10, 12m, x.PayableAccount);

        var bom = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, product, components: [Component(part, 2m)])), HttpStatusCode.Created);
        Assert.True((bool)bom["components"]![0]!["useCurrentCost"]!);
        var bomId = (string)bom["id"]!;

        Assert.Equal(24m, (decimal)(await NewOrderAsync(x, 1m, bomId: bomId))["estimatedTotalCost"]!);
        Assert.Equal(12m, (decimal)(await Ok(await x.C.GetAsync($"{Mfg}/costing/rollup/{bomId}?warehouseId={x.Warehouse}")))["componentBreakdown"]![0]!["unitCost"]!);

        // A later, dearer receipt moves the average, and the BOM with it: (10 x 12 + 10 x 16) / 20.
        await ReceiveAsync(x.C, x.Warehouse, part, 10, 16m, x.PayableAccount);
        Assert.Equal(14m, (decimal)(await Ok(await x.C.GetAsync($"{Mfg}/costing/rollup/{bomId}?warehouseId={x.Warehouse}")))["componentBreakdown"]![0]!["unitCost"]!);
        Assert.Equal(28m, (decimal)(await NewOrderAsync(x, 1m, bomId: bomId))["estimatedTotalCost"]!);

        // A typed cost stays what was typed.
        var typed = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, await ItemAsync(x.C, "PROD"), components: [Component(part, 2m, cost: 9m)])), HttpStatusCode.Created);
        Assert.False((bool)typed["components"]![0]!["useCurrentCost"]!);
        Assert.Equal(9m, (decimal)typed["components"]![0]!["unitCost"]!);
    }

    [Fact]
    public async Task Indirect_bom_loops_are_refused_on_create_and_on_reactivation()
    {
        var x = await SetupAsync();
        var a = await ItemAsync(x.C, "A");
        var b = await ItemAsync(x.C, "B");
        var c = await ItemAsync(x.C, "C");

        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, a, components: [Component(b, 1m, 1m)])), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, b, components: [Component(c, 1m, 1m)])), HttpStatusCode.Created);

        // C uses A, A uses B, B uses C.
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, c, components: [Component(a, 1m, 1m)]))).StatusCode);

        var parked = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x.Entity, c, components: [Component(a, 1m, 1m)], isActive: false)), HttpStatusCode.Created);
        var reactivate = new { name = "Test BOM", outputQuantity = 1m, uom = "pcs", version = "1.0", laborCostPerUnit = 0m, overheadCostPerUnit = 0m, isActive = true, components = (object?)null };
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Mfg}/boms/{(string)parked["id"]!}", reactivate)).StatusCode);
    }
}
