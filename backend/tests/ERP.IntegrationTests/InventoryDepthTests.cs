using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Inventory depth: warehouse maintenance, backdated issues, physical counts, reports, search and low-stock reminders.</summary>
public class InventoryDepthTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Inv = "/api/v1/m/inventory";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Wh, string Item, string Sku, string Offset);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "InvD " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = "W" + Guid.NewGuid().ToString("N")[..6], name = "Depth WH", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var sku = "DEP-" + Guid.NewGuid().ToString("N")[..6];
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku, name = "Depth item " + sku, type = "product", uom = "pcs", isActive = true }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        return new Ctx(c, entity, wh, item, sku, (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!);
    }

    private static Task Receive(Ctx x, decimal qty, decimal cost, string date, string? reference = null)
        => x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = x.Wh, itemId = x.Item, quantity = qty, unitCost = cost, date, offsetAccountId = x.Offset, reference });

    private static async Task<decimal> BalanceAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    [Fact]
    public async Task Warehouses_can_be_renamed_and_deactivated_and_codes_stay_unique()
    {
        var x = await SetupAsync();
        var code = "U" + Guid.NewGuid().ToString("N")[..6];
        var created = await Ok(await x.C.PostAsJsonAsync($"{Inv}/warehouses", new { code, name = "Original", legalEntityId = x.Entity }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/warehouses", new { code, name = "Dup", legalEntityId = x.Entity })).StatusCode);
        var id = (string)created["id"]!;
        var updated = await Ok(await x.C.PutAsJsonAsync($"{Inv}/warehouses/{id}", new { name = "Renamed", address = "Dock 4", isActive = false }));
        Assert.Equal("Renamed", (string)updated["name"]!);
        Assert.False((bool)updated["isActive"]!);
        Assert.Equal(code, (string)updated["code"]!); // the code never changes
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = id, itemId = x.Item, quantity = 1, unitCost = 1m, date = "2026-05-01", offsetAccountId = x.Offset })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Inv}/warehouses/{id}", new { name = "", isActive = true })).StatusCode);
    }

    [Fact]
    public async Task An_issue_dated_before_the_stock_arrived_is_rejected()
    {
        var x = await SetupAsync();
        await Receive(x, 10, 5m, "2026-06-10");
        var early = await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh, itemId = x.Item, quantity = 2, date = "2026-06-01" });
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        await Ok(await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh, itemId = x.Item, quantity = 2, date = "2026-06-10" }), HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_stock_count_snapshots_posts_one_adjustment_per_difference_and_is_idempotent()
    {
        var x = await SetupAsync();
        await Receive(x, 10, 20m, "2026-07-01");
        var other = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/items", new { sku = "DEP2-" + Guid.NewGuid().ToString("N")[..6], name = "Found item", type = "product", uom = "pcs", isActive = true }), HttpStatusCode.Created))["id"]!;

        var count = await Ok(await x.C.PostAsJsonAsync($"{Inv}/counts", new { warehouseId = x.Wh, countDate = "2026-07-31", memo = "Month end" }), HttpStatusCode.Created);
        var id = (string)count["id"]!;
        Assert.StartsWith("CNT-", (string)count["number"]!);
        var line = Assert.Single(count["lines"]!.AsArray())!;
        Assert.Equal(10m, (decimal)line["systemQuantity"]!);
        Assert.Null((decimal?)line["countedQuantity"]);

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Inv}/counts/{id}/post", null)).StatusCode); // nothing counted yet

        // 8 counted of 10 (shortage), and an item that was not on the sheet turns up (3 found)
        var saved = await Ok(await x.C.PutAsJsonAsync($"{Inv}/counts/{id}/lines", new { lines = new object[] { new { itemId = x.Item, countedQuantity = 8m }, new { itemId = other, countedQuantity = 3m } } }));
        Assert.Equal("counting", (string)saved["status"]!);
        Assert.Equal(2, saved["lines"]!.AsArray().Count);
        Assert.Equal(-2m, (decimal)saved["lines"]!.AsArray().Single(l => (string)l!["itemId"]! == x.Item)!["variance"]!);

        var posted = await Ok(await x.C.PostAsync($"{Inv}/counts/{id}/post", null));
        Assert.Equal("posted", (string)posted["status"]!);
        Assert.All(posted["lines"]!.AsArray(), l => Assert.NotNull((string?)l!["movementId"]));
        Assert.Equal(160m, await BalanceAsync(x, "1300")); // 10 x 20 less the 2 lost at average cost (200 - 40)
        var again = await Ok(await x.C.PostAsync($"{Inv}/counts/{id}/post", null)); // idempotent
        Assert.Equal("posted", (string)again["status"]!);
        Assert.Equal(160m, await BalanceAsync(x, "1300"));
        var rows = (await Ok(await x.C.GetAsync($"{Inv}/stock?warehouseId={x.Wh}")))["rows"]!.AsArray();
        Assert.Equal(8m, (decimal)rows.Single(r => (string)r!["itemId"]! == x.Item)!["quantity"]!);
        Assert.Equal(3m, (decimal)rows.Single(r => (string)r!["itemId"]! == other)!["quantity"]!);
        var moves = (await Ok(await x.C.GetAsync($"{Inv}/movements?pageSize=100&filter=reference:eq:{(string)count["number"]!}"))).AsArray();
        Assert.Equal(2, moves.Count); // exactly one adjustment per difference, found by Reference

        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Inv}/counts/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PutAsJsonAsync($"{Inv}/counts/{id}/lines", new { lines = new object[] { new { itemId = x.Item, countedQuantity = 1m } } })).StatusCode);
        Assert.Contains((await Ok(await x.C.GetAsync($"{Inv}/counts?pageSize=50"))).AsArray(), c => (string)c!["id"]! == id);
    }

    [Fact]
    public async Task A_cancelled_count_cannot_be_posted_and_services_cannot_be_counted()
    {
        var x = await SetupAsync();
        await Receive(x, 5, 10m, "2026-07-01");
        var count = await Ok(await x.C.PostAsJsonAsync($"{Inv}/counts", new { warehouseId = x.Wh, countDate = "2026-07-31" }), HttpStatusCode.Created);
        var id = (string)count["id"]!;
        var service = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/items", new { sku = "SRV-" + Guid.NewGuid().ToString("N")[..6], name = "Labour", type = "service", uom = "hr", isActive = true }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PutAsJsonAsync($"{Inv}/counts/{id}/lines", new { lines = new object[] { new { itemId = service, countedQuantity = 1m } } })).StatusCode);
        await Ok(await x.C.PostAsync($"{Inv}/counts/{id}/cancel", null));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsync($"{Inv}/counts/{id}/post", null)).StatusCode);
    }

    [Fact]
    public async Task Inventory_reports_replay_movements_for_valuation_period_summary_slow_stock_and_reorder()
    {
        var x = await SetupAsync();
        await Receive(x, 10, 10m, "2026-01-05");
        await x.C.PostAsJsonAsync($"{Inv}/stock/issue", new { warehouseId = x.Wh, itemId = x.Item, quantity = 4, date = "2026-02-10" });
        await Receive(x, 6, 20m, "2026-03-01");

        var january = await Ok(await x.C.GetAsync($"{Inv}/reports/valuation?asOf=2026-01-31"));
        var jan = january["rows"]!.AsArray().Single(r => (string)r!["sku"]! == x.Sku)!;
        Assert.Equal(10m, (decimal)jan["quantity"]!);
        Assert.Equal(100m, (decimal)jan["value"]!);
        var march = await Ok(await x.C.GetAsync($"{Inv}/reports/valuation?asOf=2026-03-31"));
        var mar = march["rows"]!.AsArray().Single(r => (string)r!["sku"]! == x.Sku)!;
        Assert.Equal(12m, (decimal)mar["quantity"]!);
        Assert.Equal(180m, (decimal)mar["value"]!); // 6 left at 10 + 6 at 20

        var summary = await Ok(await x.C.GetAsync($"{Inv}/reports/movement-summary?from=2026-02-01&to=2026-03-31"));
        var row = summary["rows"]!.AsArray().Single(r => (string)r!["sku"]! == x.Sku)!;
        Assert.Equal(10m, (decimal)row["opening"]!);
        Assert.Equal(6m, (decimal)row["receipts"]!);
        Assert.Equal(4m, (decimal)row["issues"]!);
        Assert.Equal(12m, (decimal)row["closing"]!);
        Assert.Equal(180m, (decimal)row["closingValue"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.GetAsync($"{Inv}/reports/movement-summary?from=2026-04-01")).StatusCode);

        var slow = await Ok(await x.C.GetAsync($"{Inv}/reports/slow-moving?asOf=2026-06-30&days=90"));
        var idle = slow["rows"]!.AsArray().Single(r => (string)r!["sku"]! == x.Sku)!;
        Assert.Equal(121, (int)idle["daysIdle"]!); // last movement 1 March
        Assert.DoesNotContain((await Ok(await x.C.GetAsync($"{Inv}/reports/slow-moving?asOf=2026-03-15&days=90")))["rows"]!.AsArray(), r => (string)r!["sku"]! == x.Sku);

        await Ok(await x.C.PutAsJsonAsync($"{Inv}/stock/policy", new { itemId = x.Item, reorderLevel = 20, reorderQuantity = 5 }), HttpStatusCode.NoContent);
        var reorder = (await Ok(await x.C.GetAsync($"{Inv}/reports/reorder")))["rows"]!.AsArray().Single(r => (string)r!["sku"]! == x.Sku)!;
        Assert.Equal(12m, (decimal)reorder["onHand"]!);
        Assert.Equal(8m, (decimal)reorder["suggestedOrder"]!); // shortfall to the level (8) beats the reorder quantity (5)
    }

    [Fact]
    public async Task Global_search_finds_warehouses_and_stocked_items_and_low_stock_reminds_once_a_day()
    {
        var x = await SetupAsync();
        await Receive(x, 3, 10m, DateTime.UtcNow.ToString("yyyy-MM-dd"));
        await Ok(await x.C.PutAsJsonAsync($"{Inv}/stock/policy", new { itemId = x.Item, reorderLevel = 10, reorderQuantity = 20 }), HttpStatusCode.NoContent);

        var hits = (await Ok(await x.C.GetAsync($"/api/v1/search?q={x.Sku}")))["hits"]!.AsArray();
        Assert.Contains(hits, h => (string)h!["category"]! == "Stock items" && (string)h["subtitle"]! == x.Sku);
        Assert.Contains((await Ok(await x.C.GetAsync("/api/v1/search?q=Depth%20WH")))["hits"]!.AsArray(), h => (string)h!["category"]! == "Warehouses");

        var first = (int)(await Ok(await x.C.PostAsync("/api/v1/messages/send-reminders", null)))["sent"]!;
        Assert.True(first >= 1);
        var messages = (await Ok(await x.C.GetAsync("/api/v1/messages?pageSize=200"))).AsArray();
        Assert.Contains(messages, m => ((string)m!["subject"]!).Contains("Low stock") && ((string)m["subject"]!).Contains(x.Sku));
        var second = await Ok(await x.C.PostAsync("/api/v1/messages/send-reminders", null));
        var low = (await Ok(await x.C.GetAsync("/api/v1/messages?pageSize=200"))).AsArray().Count(m => ((string)m!["subject"]!).Contains(x.Sku));
        Assert.Equal(1, low); // the same item is not reminded twice on one day (per recipient)
        Assert.True((int)second["sent"]! >= 0);
    }
}
