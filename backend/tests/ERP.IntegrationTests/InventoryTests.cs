using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Inventory: receipts, moving-average cost, issues, adjustments, transfers, valuation and the ledger postings.</summary>
public class InventoryTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Inv = "/api/v1/m/inventory";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Wh1, string Wh2, string Item, string Offset, JsonArray Accounts);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail); // permissions arrive after re-login
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Inv " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        async Task<string> Wh(string code) => (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = code + Guid.NewGuid().ToString("N")[..4], name = "WH " + code, legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku = "STK-" + Guid.NewGuid().ToString("N")[..6], name = "Filter", type = "product", uom = "pcs", isActive = true }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        return new Ctx(c, entity, await Wh("A"), await Wh("B"), item, (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!, accounts);
    }

    private static async Task<decimal> BalanceAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    [Fact]
    public async Task Stock_is_costed_at_a_moving_average_and_posts_to_the_ledger()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, unitCost = 100m, date = "2026-03-01", offsetAccountId = x.Offset }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, unitCost = 200m, date = "2026-03-05", offsetAccountId = x.Offset }), HttpStatusCode.Created);
        Assert.Equal(3000m, await BalanceAsync(x, "1300"));

        var row = (await Ok(await x.C.GetAsync($"{Inv}/stock?warehouseId={x.Wh1}")))["rows"]!.AsArray().Single()!;
        Assert.Equal(20m, (decimal)row["quantity"]!);
        Assert.Equal(150m, (decimal)row["averageCost"]!);

        var issue = await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 5, date = "2026-03-10" }), HttpStatusCode.Created);
        Assert.Equal(-5m, (decimal)issue["quantity"]!);
        Assert.Equal(150m, (decimal)issue["unitCost"]!);
        Assert.Equal(2250m, await BalanceAsync(x, "1300"));
        Assert.Equal(750m, await BalanceAsync(x, "5000")); // cost of goods sold

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 100, date = "2026-03-11" })).StatusCode);
        var report = await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={x.Item}"));
        Assert.Equal(2250m, (decimal)report["totalValue"]!);
    }

    [Fact]
    public async Task Transfers_adjustments_and_reorder_levels_work()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, unitCost = 50m, date = "2026-04-01", offsetAccountId = x.Offset }), HttpStatusCode.Created);

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/transfer", new { fromWarehouseId = x.Wh1, toWarehouseId = x.Wh2, itemId = x.Item, quantity = 4, date = "2026-04-02" }), HttpStatusCode.Created);
        var rows = (await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={x.Item}")))["rows"]!.AsArray();
        Assert.Equal(6m, (decimal)rows.Single(r => (string)r!["warehouseId"]! == x.Wh1)!["quantity"]!);
        Assert.Equal(4m, (decimal)rows.Single(r => (string)r!["warehouseId"]! == x.Wh2)!["quantity"]!);
        Assert.Equal(500m, await BalanceAsync(x, "1300")); // transfers do not touch the ledger
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Inv}/stock/transfer", new { fromWarehouseId = x.Wh1, toWarehouseId = x.Wh1, itemId = x.Item, quantity = 1, date = "2026-04-02" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/transfer", new { fromWarehouseId = x.Wh2, toWarehouseId = x.Wh1, itemId = x.Item, quantity = 5, date = "2026-04-03" })).StatusCode);

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/adjust", new { warehouseId = x.Wh1, itemId = x.Item, quantityDelta = -2, date = "2026-04-04", memo = "damaged" }), HttpStatusCode.Created);
        Assert.Equal(400m, await BalanceAsync(x, "1300")); // 8 left at cost 50
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Inv}/stock/adjust", new { warehouseId = x.Wh1, itemId = x.Item, quantityDelta = 0, date = "2026-04-04" })).StatusCode);

        Assert.Empty((await Ok(await x.C.GetAsync($"{Inv}/stock/low"))).AsArray());
        await Ok(await x.C.PutAsJsonAsync($"{Inv}/stock/policy", new { itemId = x.Item, reorderLevel = 10, reorderQuantity = 50 }), HttpStatusCode.NoContent);
        var low = (await Ok(await x.C.GetAsync($"{Inv}/stock/low"))).AsArray().Single()!;
        Assert.Equal(8m, (decimal)low["onHand"]!);

        var moves = (await Ok(await x.C.GetAsync($"{Inv}/movements?pageSize=100&filter=itemId:eq:{x.Item}"))).AsArray();
        Assert.Equal(4, moves.Count); // receipt, transfer out and in, adjustment
    }

    [Fact]
    public async Task Services_cannot_be_stocked_and_the_module_must_be_enabled()
    {
        var x = await SetupAsync();
        var service = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/items", new { sku = "SRV-" + Guid.NewGuid().ToString("N")[..6], name = "Labour", type = "service", uom = "hr", isActive = true }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = service, quantity = 1, unitCost = 1m, date = "2026-04-01", offsetAccountId = x.Offset })).StatusCode);
        var (owner, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re"); // inventory is not enabled there
        Assert.NotEqual(HttpStatusCode.OK, (await owner.GetAsync($"{Inv}/stock")).StatusCode);
    }

    [Fact]
    public async Task A_backdated_stock_out_cannot_use_stock_that_later_movements_already_consumed()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, unitCost = 10m, date = "2026-01-01", offsetAccountId = x.Offset }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, date = "2026-01-05" }), HttpStatusCode.Created);
        var before = await BalanceAsync(x, "1300");

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 5, date = "2026-01-02" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/adjust", new { warehouseId = x.Wh1, itemId = x.Item, quantityDelta = -5, date = "2026-01-02" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/transfer", new { fromWarehouseId = x.Wh1, toWarehouseId = x.Wh2, itemId = x.Item, quantity = 5, date = "2026-01-02" })).StatusCode);
        Assert.Equal(before, await BalanceAsync(x, "1300")); // nothing was posted

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 10, unitCost = 10m, date = "2026-01-03", offsetAccountId = x.Offset }), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 5, date = "2026-01-04" }), HttpStatusCode.Created); // still within stock on every later date
    }

    [Fact]
    public async Task Stock_commands_reject_missing_dates_empty_ids_and_extra_decimals()
    {
        var x = await SetupAsync();
        async Task<HttpStatusCode> Post(string path, object body) => (await x.C.PostAsJsonAsync($"{Inv}/stock/{path}", body)).StatusCode;

        // omitted date deserialises to 0001-01-01
        Assert.Equal(HttpStatusCode.BadRequest, await Post("receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 1, unitCost = 1m, offsetAccountId = x.Offset }));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("issue", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("adjust", new { warehouseId = x.Wh1, itemId = x.Item, quantityDelta = 1 }));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("transfer", new { fromWarehouseId = x.Wh1, toWarehouseId = x.Wh2, itemId = x.Item, quantity = 1 }));

        Assert.Equal(HttpStatusCode.BadRequest, await Post("receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 1.00001m, unitCost = 1m, date = "2026-05-01", offsetAccountId = x.Offset }));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 1, unitCost = 1.00001m, date = "2026-05-01", offsetAccountId = x.Offset }));
        Assert.Equal(HttpStatusCode.BadRequest, await Post("transfer", new { fromWarehouseId = Guid.Empty, toWarehouseId = x.Wh2, itemId = Guid.Empty, quantity = 1, date = "2026-05-01" }));

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 1.5m, unitCost = 2.25m, date = "2026-05-01", offsetAccountId = x.Offset }), HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_warehouse_that_holds_stock_cannot_be_deactivated()
    {
        var x = await SetupAsync();
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh1, itemId = x.Item, quantity = 5, unitCost = 10m, date = "2026-05-01", offsetAccountId = x.Offset }), HttpStatusCode.Created);

        async Task<bool> IsActive() => (bool)(await Ok(await x.C.GetAsync($"{Inv}/warehouses?pageSize=100"))).AsArray().Single(w => (string)w!["id"]! == x.Wh1)!["isActive"]!;
        object Update(bool active) => new { name = "Renamed", address = (string?)null, isActive = active };
        await Ok(await x.C.PutAsJsonAsync($"{Inv}/warehouses/{x.Wh1}", Update(true))); // renaming with stock is fine
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Inv}/warehouses/{x.Wh1}", Update(false))).StatusCode);
        Assert.True(await IsActive());

        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/transfer", new { fromWarehouseId = x.Wh1, toWarehouseId = x.Wh2, itemId = x.Item, quantity = 5, date = "2026-05-02" }), HttpStatusCode.Created);
        Assert.False((bool)(await Ok(await x.C.PutAsJsonAsync($"{Inv}/warehouses/{x.Wh1}", Update(false))))["isActive"]!);
        Assert.False(await IsActive());
    }
}
