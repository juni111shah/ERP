using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

public class ManufacturingWorkflowTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Inv = "/api/v1/m/inventory";
    private const string Mfg = "/api/v1/m/manufacturing";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(
        HttpClient C,
        string Entity,
        string Warehouse,
        string ComponentItem,
        string ProducedItem,
        string BomId);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/manufacturing/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "Mfg " + Guid.NewGuid().ToString("N")[..6],
            countryCode = "AE",
            baseCurrency = "AED",
            isTaxRegistered = false
        }), HttpStatusCode.Created))["id"]!;

        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new
        {
            code = "WH-" + Guid.NewGuid().ToString("N")[..4],
            name = "Main Warehouse",
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        var compItem = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku = "RAW-" + Guid.NewGuid().ToString("N")[..6],
            name = "Raw Sheet Metal",
            type = "product",
            uom = "pcs",
            purchasePrice = 50m,
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        var prodItem = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku = "FG-" + Guid.NewGuid().ToString("N")[..6],
            name = "Finished Metal Box",
            type = "product",
            uom = "pcs",
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        // Initial stock: receive 20 pcs of component at 50 AED each = 1000 AED
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        var offset = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Inv}/stock/receive", new
        {
            warehouseId = wh,
            itemId = compItem,
            quantity = 20,
            unitCost = 50m,
            date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            offsetAccountId = offset
        }), HttpStatusCode.Created);

        // BOM: 1 FG requires 2 RAW (unit cost 50 each = 100 material) + 10 labor + 5 overhead = 115 total unit cost
        var bom = await Ok(await c.PostAsJsonAsync($"{Mfg}/boms", new
        {
            legalEntityId = entity,
            code = "BOM-" + Guid.NewGuid().ToString("N")[..6],
            name = "Metal Box BOM",
            producedItemId = prodItem,
            producedItemCode = "FG-BOX",
            producedItemName = "Finished Metal Box",
            outputQuantity = 1,
            uom = "pcs",
            version = "1.0",
            laborCostPerUnit = 10m,
            overheadCostPerUnit = 5m,
            isActive = true,
            components = new[]
            {
                new
                {
                    componentItemId = compItem,
                    componentItemCode = "RAW-SHEET",
                    componentItemName = "Raw Sheet Metal",
                    quantityRequired = 2m,
                    uom = "pcs",
                    scrapPercentage = 0m,
                    unitCost = 50m
                }
            }
        }), HttpStatusCode.Created);

        return new Ctx(c, entity, wh, compItem, prodItem, (string)bom["id"]!);
    }

    private static async Task<decimal> BalanceAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    private static async Task<(decimal Debit, decimal Credit)> TrialBalanceTotalsAsync(Ctx x)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        return ((decimal)tb["totalDebit"]!, (decimal)tb["totalCredit"]!);
    }

    [Fact]
    public async Task Second_complete_call_returns_409_and_posts_nothing()
    {
        var x = await SetupAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var wo = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders", new
        {
            legalEntityId = x.Entity,
            bomId = x.BomId,
            plannedQuantity = 1,
            warehouseId = x.Warehouse,
            startDate = today,
            dueDate = today
        }), HttpStatusCode.Created);
        var woId = (string)wo["id"]!;

        // Cannot complete from Planned
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Mfg}/workorders/{woId}/complete", new
        {
            completedQuantity = 1,
            scrappedQuantity = 0,
            actualTotalCost = 0
        })).StatusCode);

        // Release to InProgress
        await Ok(await x.C.PostAsync($"{Mfg}/workorders/{woId}/release", null));

        // First complete succeeds
        await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{woId}/complete", new
        {
            completedQuantity = 1,
            scrappedQuantity = 0,
            actualTotalCost = 0
        }));

        var movementsBefore = (await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={x.ProducedItem}")))["rows"]!.AsArray().Count;

        // Second complete call returns 409
        var second = await x.C.PostAsJsonAsync($"{Mfg}/workorders/{woId}/complete", new
        {
            completedQuantity = 1,
            scrappedQuantity = 0,
            actualTotalCost = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        // Posts nothing
        var movementsAfter = (await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={x.ProducedItem}")))["rows"]!.AsArray().Count;
        Assert.Equal(movementsBefore, movementsAfter);
    }

    [Fact]
    public async Task Completion_calculates_cost_and_posts_balanced_journal_leaving_cogs_unchanged()
    {
        var x = await SetupAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var cogsBefore = await BalanceAsync(x, "5000");

        var wo = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders", new
        {
            legalEntityId = x.Entity,
            bomId = x.BomId,
            plannedQuantity = 1,
            warehouseId = x.Warehouse,
            startDate = today,
            dueDate = today
        }), HttpStatusCode.Created);
        var woId = (string)wo["id"]!;

        await Ok(await x.C.PostAsync($"{Mfg}/workorders/{woId}/release", null));

        var completed = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders/{woId}/complete", new
        {
            completedQuantity = 1,
            scrappedQuantity = 0,
            actualTotalCost = 0 // Ignored: calculated from materials (100) + conversion (15) = 115
        }));

        Assert.Equal(115m, (decimal)completed["actualTotalCost"]!);

        // Finished-goods inventory value equals consumed material cost plus conversion cost
        // Raw material inventory (1300): was 1000, consumed 100 (Cr 100), finished received 115 (Dr 115) -> net 1015
        var invBalance = await BalanceAsync(x, "1300");
        Assert.Equal(1015m, invBalance);

        // Trial balance still sums to zero
        var (debit, credit) = await TrialBalanceTotalsAsync(x);
        Assert.Equal(debit, credit);

        // COGS is unchanged
        var cogsAfter = await BalanceAsync(x, "5000");
        Assert.Equal(cogsBefore, cogsAfter);
    }

    [Fact]
    public async Task Failure_rolls_back_status_change_when_component_is_not_product()
    {
        var x = await SetupAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // A BOM can only be created from products, so the item is turned into a service afterwards
        var serviceSku = "SRV-" + Guid.NewGuid().ToString("N")[..6];
        var serviceItem = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/items", new
        {
            sku = serviceSku,
            name = "Assembly Consulting",
            type = "product",
            uom = "hr",
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        // BOM containing service item as component
        var badBom = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", new
        {
            legalEntityId = x.Entity,
            code = "BOM-BAD-" + Guid.NewGuid().ToString("N")[..6],
            name = "Bad BOM",
            producedItemId = x.ProducedItem,
            producedItemCode = "FG-BOX",
            producedItemName = "Finished Metal Box",
            outputQuantity = 1,
            uom = "pcs",
            version = "1.0",
            isActive = true,
            components = new[]
            {
                new
                {
                    componentItemId = serviceItem,
                    componentItemCode = "SRV-CONS",
                    componentItemName = "Assembly Consulting",
                    quantityRequired = 1m,
                    uom = "hr",
                    scrapPercentage = 0m,
                    unitCost = 100m
                }
            }
        }), HttpStatusCode.Created);

        await Ok(await x.C.PutAsJsonAsync($"/api/v1/items/{serviceItem}", new { sku = serviceSku, name = "Assembly Consulting", type = "service", uom = "hr", isActive = true }));

        var wo = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders", new
        {
            legalEntityId = x.Entity,
            bomId = (string)badBom["id"]!,
            plannedQuantity = 1,
            warehouseId = x.Warehouse,
            startDate = today,
            dueDate = today
        }), HttpStatusCode.Created);
        var woId = (string)wo["id"]!;

        await Ok(await x.C.PostAsync($"{Mfg}/workorders/{woId}/release", null));

        // Complete should fail with 409 because component is a service, not a product
        var res = await x.C.PostAsJsonAsync($"{Mfg}/workorders/{woId}/complete", new
        {
            completedQuantity = 1,
            scrappedQuantity = 0,
            actualTotalCost = 0
        });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        // Any failure rolls back the status change -> still InProgress
        var woFetched = await Ok(await x.C.GetAsync($"{Mfg}/workorders/{woId}"));
        Assert.Equal("InProgress", (string)woFetched["status"]!, ignoreCase: true);
    }

    private static object BomBody(Ctx x, string code, string produced, object[] components) => new
    {
        legalEntityId = x.Entity, code, name = "Test BOM", producedItemId = produced, producedItemCode = "IGNORED", producedItemName = "Ignored",
        outputQuantity = 1, uom = "pcs", version = "1.0", laborCostPerUnit = 0m, overheadCostPerUnit = 0m, isActive = true, components,
    };

    private static object Component(string item, decimal qty = 1m, decimal scrap = 0m, decimal cost = 5m)
        => new { componentItemId = item, componentItemCode = "X", componentItemName = "X", quantityRequired = qty, uom = "pcs", scrapPercentage = scrap, unitCost = cost };

    [Fact]
    public async Task A_bom_validates_its_components_and_takes_names_from_the_catalog()
    {
        var x = await SetupAsync();
        var code = "V-" + Guid.NewGuid().ToString("N")[..6];
        async Task<HttpStatusCode> Create(string c, string produced, params object[] comps) => (await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x, c, produced, comps))).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ComponentItem, qty: 0m)));
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ComponentItem, qty: -5m)));
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ComponentItem, scrap: 150m)));
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ComponentItem, cost: -1m)));
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ComponentItem), Component(x.ComponentItem))); // duplicate component
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(x.ProducedItem)));                              // its own component
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(Guid.NewGuid().ToString())));                   // unknown item
        var service = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/items", new { sku = "SRV-" + Guid.NewGuid().ToString("N")[..6], name = "Labour", type = "service", uom = "hr", isActive = true }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, x.ProducedItem, Component(service)));
        Assert.Equal(HttpStatusCode.BadRequest, await Create(code, service, Component(x.ComponentItem)));

        var bom = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x, code, x.ProducedItem, [Component(x.ComponentItem)])), HttpStatusCode.Created);
        Assert.NotEqual("IGNORED", (string)bom["producedItemCode"]!);
        Assert.NotEqual("X", (string)bom["components"]![0]!["componentItemCode"]!);
        Assert.Equal(HttpStatusCode.Conflict, await Create(code.ToLowerInvariant(), x.ProducedItem, Component(x.ComponentItem))); // same code, any case
    }

    [Fact]
    public async Task Work_orders_need_an_active_bom_and_a_usable_warehouse_and_a_used_bom_cannot_be_deleted()
    {
        var x = await SetupAsync();
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        object Wo(string bom, string warehouse, string? entity = null) => new { legalEntityId = entity ?? x.Entity, bomId = bom, plannedQuantity = 1, warehouseId = warehouse, startDate = today, dueDate = today };
        async Task<HttpStatusCode> Create(object body) => (await x.C.PostAsJsonAsync($"{Mfg}/workorders", body)).StatusCode;

        Assert.Equal(HttpStatusCode.BadRequest, await Create(Wo(x.BomId, Guid.NewGuid().ToString())));          // unknown warehouse
        var otherEntity = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Mfg2 " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, await Create(Wo(x.BomId, x.Warehouse, otherEntity)));            // BOM belongs to another entity
        var otherWh = (string)(await Ok(await x.C.PostAsJsonAsync($"{Inv}/warehouses", new { code = "O-" + Guid.NewGuid().ToString("N")[..4], name = "Other", legalEntityId = otherEntity }), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, await Create(Wo(x.BomId, otherWh)));                             // warehouse of another entity

        var wo = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/workorders", Wo(x.BomId, x.Warehouse)), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.DeleteAsync($"{Mfg}/boms/{x.BomId}")).StatusCode);     // in use

        var bom = await Ok(await x.C.GetAsync($"{Mfg}/boms/{x.BomId}"));
        var deactivate = new
        {
            name = (string)bom["name"]!, outputQuantity = (decimal)bom["outputQuantity"]!, uom = (string)bom["uom"]!, version = (string)bom["version"]!, laborCostPerUnit = 0m, overheadCostPerUnit = 0m, isActive = false,
            components = (JsonNode?)null,
        };
        var upd = await Ok(await x.C.PutAsJsonAsync($"{Mfg}/boms/{x.BomId}", deactivate));
        Assert.False((bool)upd["isActive"]!, upd.ToJsonString());
        Assert.Equal(HttpStatusCode.Conflict, await Create(Wo(x.BomId, x.Warehouse)));                           // inactive BOM

        var spare = await Ok(await x.C.PostAsJsonAsync($"{Mfg}/boms", BomBody(x, "DEL-" + Guid.NewGuid().ToString("N")[..6], x.ProducedItem, [Component(x.ComponentItem)])), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Mfg}/boms/{(string)spare["id"]!}")).StatusCode); // unused BOM can go
        Assert.NotNull(wo);
    }
}
