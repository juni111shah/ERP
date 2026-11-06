using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Trip completion on the ledger: cost from the stock issue, revenue and the receivable from an optional invoice, and every loaded unit accounted for.</summary>
public class DistributionTripLedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Dist = "/api/v1/m/distribution";

    private async Task<(SupplyChainKit K, string Route)> SetupAsync()
    {
        var k = await NewAsync(f, modules: ["inventory", "distribution"]);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var route = (string)(await Ok(await k.C.PostAsJsonAsync($"{Dist}/routes", new { legalEntityId = k.Entity, code = "R" + tag, name = "North", isActive = true }), HttpStatusCode.Created))["id"]!;
        return (k, route);
    }

    private static async Task<string> TripAsync(SupplyChainKit k, string route, decimal loaded = 10m, decimal price = 30m, string? warehouse = null)
        => (string)(await Ok(await k.C.PostAsJsonAsync($"{Dist}/trips", new
        {
            legalEntityId = k.Entity, routeId = route, driverUserId = Guid.NewGuid(), driverName = "Sam", vehicleNumber = "V-1", warehouseId = warehouse ?? k.Warehouse, departureDate = "2026-05-01",
            items = new[] { new { itemId = k.Item, itemCode = "X", itemName = "X", uom = "ea", loadedQuantity = loaded, unitPrice = price } },
        }), HttpStatusCode.Created))["id"]!;

    private static Task<HttpResponseMessage> CompleteAsync(SupplyChainKit k, string trip, decimal sold, decimal returned, decimal damaged = 0m, string? customer = null, bool allowUnaccounted = false)
        => k.C.PostAsJsonAsync($"{Dist}/trips/{trip}/complete", new
        {
            tripId = trip, totalSalesAmount = 0m, totalCollectionsAmount = 0m, customerPartyId = customer, allowUnaccounted,
            items = new[] { new { itemId = k.Item, soldQuantity = sold, returnedQuantity = returned, damagedQuantity = damaged } },
        });

    [Fact]
    public async Task Completing_with_a_customer_invoices_what_was_sold_and_posts_revenue_cost_and_receivable()
    {
        var (k, route) = await SetupAsync();
        await k.OpeningStockAsync(20m, 10m);
        var trip = await TripAsync(k, route);

        var done = await Ok(await CompleteAsync(k, trip, sold: 6m, returned: 3m, damaged: 1m, customer: k.Customer));

        Assert.Equal("completed", (string)done["status"]!, ignoreCase: true);
        var invoiceId = (string)done["invoiceId"]!;
        Assert.Equal(180m, (decimal)done["totalSalesAmount"]!);
        var invoice = await Ok(await k.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal("issued", (string)invoice["status"]!);
        Assert.Equal(180m, (decimal)invoice["total"]!);
        Assert.Equal(k.Customer, (string)invoice["partyId"]!);
        Assert.Equal(trip, (string)invoice["sourceId"]!);
        Assert.Equal(6m, (decimal)invoice["lines"]![0]!["quantity"]!);

        Assert.Equal(180m, await k.BalanceAsync("ar.control"));
        Assert.Equal(-180m, await k.BalanceAsync("income.sales"));
        Assert.Equal(70m, await k.BalanceAsync("cogs")); // sold and damaged, at cost
        Assert.Equal((13m, 130m), await k.StockAsync());
    }

    [Fact]
    public async Task Completing_with_no_customer_posts_only_the_cost_as_before()
    {
        var (k, route) = await SetupAsync();
        await k.OpeningStockAsync(20m, 10m);
        var trip = await TripAsync(k, route);

        var done = await Ok(await CompleteAsync(k, trip, sold: 10m, returned: 0m));

        Assert.Null(done["invoiceId"]);
        Assert.Equal(100m, await k.BalanceAsync("cogs"));
        Assert.Equal(0m, await k.BalanceAsync("ar.control"));
    }

    [Fact]
    public async Task Unaccounted_quantity_is_an_error_unless_explicitly_allowed()
    {
        var (k, route) = await SetupAsync();
        await k.OpeningStockAsync(20m, 10m);
        var trip = await TripAsync(k, route);

        var res = await CompleteAsync(k, trip, sold: 6m, returned: 3m);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("allowUnaccounted", await res.Content.ReadAsStringAsync());
        Assert.Equal("inProgress", (string)(await Ok(await k.C.GetAsync($"{Dist}/trips/{trip}")))["status"]!, ignoreCase: true);
        Assert.Equal(0m, await k.BalanceAsync("cogs"));

        // an empty reconciliation accounts for nothing
        var empty = await k.C.PostAsJsonAsync($"{Dist}/trips/{trip}/complete", new { tripId = trip, totalSalesAmount = 0m, totalCollectionsAmount = 0m, items = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        await Ok(await CompleteAsync(k, trip, sold: 6m, returned: 3m, allowUnaccounted: true));
        Assert.Equal(60m, await k.BalanceAsync("cogs"));
    }

    [Fact]
    public async Task A_trip_cannot_sell_more_than_the_warehouse_holds()
    {
        var (k, route) = await SetupAsync();
        await k.OpeningStockAsync(4m, 10m);
        var trip = await TripAsync(k, route);

        var res = await CompleteAsync(k, trip, sold: 6m, returned: 4m, customer: k.Customer);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("inProgress", (string)(await Ok(await k.C.GetAsync($"{Dist}/trips/{trip}")))["status"]!, ignoreCase: true);
        Assert.Equal(0m, await k.BalanceAsync("ar.control"));
        Assert.Equal(0m, await k.BalanceAsync("cogs"));
        Assert.Equal((4m, 40m), await k.StockAsync());
    }

    [Fact]
    public async Task A_trip_needs_a_usable_warehouse_and_a_real_customer()
    {
        var (k, route) = await SetupAsync();
        var other = await NewEntityAsync(k.C, "Other " + Guid.NewGuid().ToString("N")[..6]);
        var foreign = await NewWarehouseAsync(k.C, other);
        var bad = await k.C.PostAsJsonAsync($"{Dist}/trips", new
        {
            legalEntityId = k.Entity, routeId = route, driverUserId = Guid.NewGuid(), driverName = "Sam", vehicleNumber = "V-1", warehouseId = foreign, departureDate = "2026-05-01",
            items = new[] { new { itemId = k.Item, itemCode = "X", itemName = "X", uom = "ea", loadedQuantity = 1m, unitPrice = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("warehouseId", await bad.Content.ReadAsStringAsync());

        await k.OpeningStockAsync(20m, 10m);
        var trip = await TripAsync(k, route);
        var notACustomer = await CompleteAsync(k, trip, sold: 10m, returned: 0m, customer: k.Vendor);
        Assert.Equal(HttpStatusCode.BadRequest, notACustomer.StatusCode);
        Assert.Equal(0m, await k.BalanceAsync("cogs")); // rolled back with the invoice
        Assert.Equal((20m, 200m), await k.StockAsync());
    }

    [Fact]
    public async Task A_customer_over_their_credit_limit_stops_the_trip_invoice()
    {
        var (k, route) = await SetupAsync();
        await k.OpeningStockAsync(20m, 10m);
        var limited = (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/parties", new { name = "Limited " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", creditLimit = 10m }), HttpStatusCode.Created))["id"]!;
        var trip = await TripAsync(k, route);

        var res = await CompleteAsync(k, trip, sold: 10m, returned: 0m, customer: limited);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("credit limit", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await k.BalanceAsync("ar.control"));
    }
}
