using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Distribution van-sales trips: creation checks and completion (reconciliation) checks.</summary>
public class DistributionTripTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Dist = "/api/v1/m/distribution";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Warehouse, string Route, string Item, string Sku);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/distribution/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Dist " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var wh = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new { code = "W" + tag, name = "Van WH", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var route = (string)(await Ok(await c.PostAsJsonAsync($"{Dist}/routes", new { legalEntityId = entity, code = "R" + tag, name = "North" }), HttpStatusCode.Created))["id"]!;
        var sku = "SKU-" + tag;
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku, name = "Water 1L", type = "Product", uom = "ea" }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity, wh, route, item, sku);
    }

    private static object Trip(Ctx x, string? route = null, object[]? items = null, string? entity = null)
        => new
        {
            legalEntityId = entity ?? x.Entity, routeId = route ?? x.Route, driverUserId = Guid.NewGuid(), driverName = "Sam", vehicleNumber = "DXB-1", warehouseId = x.Warehouse, departureDate = "2026-05-01",
            items = items ?? [new { itemId = x.Item, itemCode = "WRONG", itemName = "Wrong name", uom = "ea", loadedQuantity = 10m, unitPrice = 2m }],
        };

    [Fact]
    public async Task A_trip_needs_a_real_route_and_catalog_items_with_sensible_quantities()
    {
        var x = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x, route: Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x, entity: Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x, items: [new { itemId = Guid.NewGuid(), itemCode = "X", itemName = "X", uom = "ea", loadedQuantity = 1m, unitPrice = 1m }]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x, items: [new { itemId = x.Item, itemCode = "X", itemName = "X", uom = "ea", loadedQuantity = 0m, unitPrice = 1m }]))).StatusCode);
        var twice = new { itemId = x.Item, itemCode = "X", itemName = "X", uom = "ea", loadedQuantity = 1m, unitPrice = 1m };
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x, items: [twice, twice]))).StatusCode);

        var trip = await Ok(await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x)), HttpStatusCode.Created);
        var line = trip["inventoryItems"]!.AsArray().Single()!;
        Assert.Equal((x.Sku, "Water 1L"), ((string)line["itemCode"]!, (string)line["itemName"]!)); // taken from the catalog
    }

    [Fact]
    public async Task A_trip_completes_only_with_a_reconciliation_that_matches_what_was_loaded()
    {
        var x = await SetupAsync();
        var trip = await Ok(await x.C.PostAsJsonAsync($"{Dist}/trips", Trip(x)), HttpStatusCode.Created); // 10 loaded
        var id = (string)trip["id"]!;
        object Body(object[] items, decimal sales = 0m) => new { tripId = id, totalSalesAmount = sales, totalCollectionsAmount = 0m, items };
        object Line(decimal sold, decimal returned, decimal damaged, string? item = null) => new { itemId = item ?? x.Item, soldQuantity = sold, returnedQuantity = returned, damagedQuantity = damaged };

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(6m, 5m, 0m)]))).StatusCode); // 11 of 10
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(-1m, 0m, 0m)]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(0m, 1m, 0m, Guid.NewGuid().ToString())]))).StatusCode); // not on the trip
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(0m, 1m, 0m)], sales: -5m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(0m, 1m, 0m), Line(0m, 1m, 0m)]))).StatusCode); // duplicate line
        Assert.Equal("inProgress", ((string)(await Ok(await x.C.GetAsync($"{Dist}/trips/{id}")))["status"]!), ignoreCase: true); // nothing half done

        var done = await Ok(await x.C.PostAsJsonAsync($"{Dist}/trips/{id}/complete", Body([Line(0m, 10m, 0m)])));
        Assert.Equal("completed", (string)done["status"]!, ignoreCase: true);
        Assert.Equal(10m, (decimal)done["inventoryItems"]!.AsArray().Single()!["returnedQuantity"]!);
    }
}
