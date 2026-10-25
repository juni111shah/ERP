using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERP.IntegrationTests;

public class ExceptionMappingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Json(HttpResponseMessage res) =>
        ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    private async Task<(HttpClient Client, string EntityId, string WarehouseId)> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        await c0.PostAsync("/api/v1/modules/services/enable", null);
        await c0.PostAsync("/api/v1/modules/crm/enable", null);
        await c0.PostAsync("/api/v1/modules/manufacturing/enable", null);
        await c0.PostAsync("/api/v1/modules/distribution/enable", null);
        await c0.PostAsync("/api/v1/modules/retail/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;

        var whRes = await c.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new
        {
            code = "WH-" + Guid.NewGuid().ToString("N")[..4],
            name = "Test Warehouse",
            legalEntityId = entity
        });
        var whId = (string)(await Json(whRes))["id"]!;

        return (c, entity, whId);
    }

    [Fact]
    public async Task Unexpected_InvalidOperation_and_KeyNotFound_and_Argument_exceptions_return_500()
    {
        using var scope = f.Services.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        var capturingLogger = new CapturingLogger<ProblemExceptionHandler>();
        var handler = new ProblemExceptionHandler(capturingLogger, env);

        // 1. InvalidOperationException -> 500
        var ctx1 = new DefaultHttpContext();
        var ms1 = new MemoryStream();
        ctx1.Response.Body = ms1;
        var handled1 = await handler.TryHandleAsync(ctx1, new InvalidOperationException("Unexpected bug"), default);
        Assert.True(handled1);
        Assert.Equal(500, ctx1.Response.StatusCode);
        Assert.True(capturingLogger.LoggedErrors.Count > 0);

        // 2. KeyNotFoundException -> 500
        var ctx2 = new DefaultHttpContext();
        var ms2 = new MemoryStream();
        ctx2.Response.Body = ms2;
        var handled2 = await handler.TryHandleAsync(ctx2, new KeyNotFoundException("Internal dictionary key missing"), default);
        Assert.True(handled2);
        Assert.Equal(500, ctx2.Response.StatusCode);

        // 3. ArgumentException -> 500
        var ctx3 = new DefaultHttpContext();
        var ms3 = new MemoryStream();
        ctx3.Response.Body = ms3;
        var handled3 = await handler.TryHandleAsync(ctx3, new ArgumentException("Internal argument bug"), default);
        Assert.True(handled3);
        Assert.Equal(500, ctx3.Response.StatusCode);
    }

    [Fact]
    public async Task Pos_order_not_found_returns_404_and_closed_session_returns_409()
    {
        var (c, entity, _) = await SetupAsync();

        // 1. Order not found -> 404
        var notFoundRes = await c.GetAsync($"/api/v1/m/pos/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Create Register and Session, close it, and attempt closing again -> 409
        var regCode = "REG-" + Guid.NewGuid().ToString("N")[..4];
        var regRes = await c.PostAsJsonAsync("/api/v1/m/pos/registers", new
        {
            legalEntityId = entity,
            code = regCode,
            name = "Test Register"
        });
        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);
        var regId = (string)(await Json(regRes))["id"]!;

        var openRes = await c.PostAsJsonAsync("/api/v1/m/pos/sessions/open", new
        {
            legalEntityId = entity,
            registerId = regId,
            cashierUserId = Guid.NewGuid(),
            cashierName = "Cashier 1",
            openingCash = 100m
        });
        Assert.Equal(HttpStatusCode.Created, openRes.StatusCode);
        var sessionId = (string)(await Json(openRes))["id"]!;

        // Close session
        var closeRes = await c.PostAsJsonAsync($"/api/v1/m/pos/sessions/{sessionId}/close", new
        {
            sessionId = Guid.Parse(sessionId),
            closedByUserId = Guid.NewGuid(),
            actualCash = 100m
        });
        Assert.Equal(HttpStatusCode.OK, closeRes.StatusCode);

        // Attempt closing already closed session -> 409
        var conflictRes = await c.PostAsJsonAsync($"/api/v1/m/pos/sessions/{sessionId}/close", new
        {
            sessionId = Guid.Parse(sessionId),
            closedByUserId = Guid.NewGuid(),
            actualCash = 100m
        });
        Assert.Equal(HttpStatusCode.Conflict, conflictRes.StatusCode);
    }

    [Fact]
    public async Task Services_not_found_returns_404_and_missing_client_invoice_returns_409()
    {
        var (c, entity, _) = await SetupAsync();

        // 1. Engagement not found -> 404
        var missingId = Guid.NewGuid();
        var notFoundRes = await c.PostAsJsonAsync($"/api/v1/m/services/engagements/{missingId}/status", new
        {
            id = missingId,
            status = "Completed"
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Milestone invoice on cancelled engagement -> 409
        var engRes = await c.PostAsJsonAsync("/api/v1/m/services/engagements", new
        {
            legalEntityId = entity,
            title = "Consulting " + Guid.NewGuid().ToString("N")[..4],
            clientId = Guid.NewGuid(),
            clientName = "Acme Client",
            type = 1,
            startDate = "2026-05-01"
        });
        Assert.Equal(HttpStatusCode.Created, engRes.StatusCode);
        var engId = (string)(await Json(engRes))["id"]!;

        var msRes = await c.PostAsJsonAsync("/api/v1/m/services/milestones", new
        {
            legalEntityId = entity,
            engagementId = Guid.Parse(engId),
            title = "Phase 1",
            amount = 1000m,
            dueDate = "2026-12-31"
        });
        Assert.Equal(HttpStatusCode.Created, msRes.StatusCode);
        var msId = (string)(await Json(msRes))["id"]!;

        var cancelRes = await c.PostAsJsonAsync($"/api/v1/m/services/engagements/{engId}/status", new
        {
            id = Guid.Parse(engId),
            status = 5 // Cancelled
        });
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);

        // Invoicing cancelled engagement -> 409 Conflict
        var invoiceRes = await c.PostAsync($"/api/v1/m/services/milestones/{msId}/invoice", null);
        Assert.Equal(HttpStatusCode.Conflict, invoiceRes.StatusCode);
    }

    [Fact]
    public async Task Retail_not_found_returns_404_and_duplicate_code_returns_409()
    {
        var (c, entity, _) = await SetupAsync();

        // 1. Price list not found -> 404
        var notFoundRes = await c.GetAsync($"/api/v1/m/retail/pricelists/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Duplicate price list code -> 409
        var code = "PL-" + Guid.NewGuid().ToString("N")[..4];
        var create1 = await c.PostAsJsonAsync("/api/v1/m/retail/pricelists", new
        {
            legalEntityId = entity,
            code,
            name = "Standard Price List",
            type = "Standard",
            currency = "AED",
            items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, create1.StatusCode);

        var create2 = await c.PostAsJsonAsync("/api/v1/m/retail/pricelists", new
        {
            legalEntityId = entity,
            code,
            name = "Duplicate Price List",
            type = "Standard",
            currency = "AED",
            items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Conflict, create2.StatusCode);
    }

    [Fact]
    public async Task Manufacturing_not_found_returns_404_and_invalid_state_returns_409()
    {
        var (c, entity, whId) = await SetupAsync();

        // 1. BOM not found -> 404
        var notFoundId = Guid.NewGuid();
        var notFoundRes = await c.PutAsJsonAsync($"/api/v1/m/manufacturing/boms/{notFoundId}", new
        {
            id = notFoundId,
            name = "Non-existent BOM",
            outputQuantity = 1m
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Releasing an already released work order -> 409
        var itemRes = await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku = "PROD-" + Guid.NewGuid().ToString("N")[..6],
            name = "Test Product",
            type = "product",
            uom = "pcs",
            isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, itemRes.StatusCode);
        var itemId = (string)(await Json(itemRes))["id"]!;

        var bomRes = await c.PostAsJsonAsync("/api/v1/m/manufacturing/boms", new
        {
            legalEntityId = entity,
            code = "BOM-" + Guid.NewGuid().ToString("N")[..6],
            name = "Test BOM",
            producedItemId = itemId,
            producedItemCode = "PROD",
            producedItemName = "Test Product",
            outputQuantity = 1m,
            uom = "pcs",
            laborCostPerUnit = 0m,
            overheadCostPerUnit = 0m,
            isActive = true,
            components = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Created, bomRes.StatusCode);
        var bomId = (string)(await Json(bomRes))["id"]!;

        var woRes = await c.PostAsJsonAsync("/api/v1/m/manufacturing/workorders", new
        {
            legalEntityId = entity,
            bomId = Guid.Parse(bomId),
            plannedQuantity = 10m,
            warehouseId = Guid.Parse(whId),
            startDate = "2026-05-01",
            dueDate = "2026-05-10"
        });
        Assert.Equal(HttpStatusCode.Created, woRes.StatusCode);
        var woId = (string)(await Json(woRes))["id"]!;

        // Release first time -> OK (transitions Planned -> Released)
        var rel1 = await c.PostAsync($"/api/v1/m/manufacturing/workorders/{woId}/release", null);
        Assert.Equal(HttpStatusCode.OK, rel1.StatusCode);

        // Release second time (already Released) -> 409 Conflict
        var rel2 = await c.PostAsync($"/api/v1/m/manufacturing/workorders/{woId}/release", null);
        Assert.Equal(HttpStatusCode.Conflict, rel2.StatusCode);
    }

    [Fact]
    public async Task Distribution_not_found_returns_404_and_completed_trip_returns_409()
    {
        var (c, entity, whId) = await SetupAsync();

        // 1. Route not found -> 404
        var notFoundId = Guid.NewGuid();
        var notFoundRes = await c.PutAsJsonAsync($"/api/v1/m/distribution/routes/{notFoundId}", new
        {
            id = notFoundId,
            name = "Non-existent Route"
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Completing already completed trip -> 409
        var routeRes = await c.PostAsJsonAsync("/api/v1/m/distribution/routes", new
        {
            legalEntityId = entity,
            code = "RT-" + Guid.NewGuid().ToString("N")[..4],
            name = "Route 1"
        });
        Assert.Equal(HttpStatusCode.Created, routeRes.StatusCode);
        var routeId = (string)(await Json(routeRes))["id"]!;

        var tripRes = await c.PostAsJsonAsync("/api/v1/m/distribution/trips", new
        {
            legalEntityId = entity,
            routeId = Guid.Parse(routeId),
            driverUserId = Guid.NewGuid(),
            driverName = "Driver 1",
            vehicleNumber = "DXB-1234",
            warehouseId = Guid.Parse(whId),
            departureDate = "2026-05-01",
            items = new[]
            {
                new
                {
                    itemId = Guid.NewGuid(),
                    itemCode = "ITM-1",
                    itemName = "Item 1",
                    uom = "pcs",
                    loadedQuantity = 10m,
                    unitPrice = 50m
                }
            }
        });
        Assert.Equal(HttpStatusCode.Created, tripRes.StatusCode);
        var tripId = (string)(await Json(tripRes))["id"]!;

        var comp1 = await c.PostAsJsonAsync($"/api/v1/m/distribution/trips/{tripId}/complete", new
        {
            tripId = Guid.Parse(tripId),
            totalSalesAmount = 0m,
            totalCollectionsAmount = 0m,
            items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, comp1.StatusCode);

        // Complete again -> 409 Conflict
        var comp2 = await c.PostAsJsonAsync($"/api/v1/m/distribution/trips/{tripId}/complete", new
        {
            tripId = Guid.Parse(tripId),
            totalSalesAmount = 0m,
            totalCollectionsAmount = 0m,
            items = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Conflict, comp2.StatusCode);
    }

    [Fact]
    public async Task Crm_not_found_returns_404_and_closed_opportunity_returns_409()
    {
        var (c, entity, _) = await SetupAsync();

        // 1. Lead not found -> 404
        var notFoundId = Guid.NewGuid();
        var notFoundRes = await c.PutAsJsonAsync($"/api/v1/m/crm/leads/{notFoundId}", new
        {
            id = notFoundId,
            fullName = "Nobody"
        });
        Assert.Equal(HttpStatusCode.NotFound, notFoundRes.StatusCode);

        // 2. Closing already closed opportunity -> 409
        var oppRes = await c.PostAsJsonAsync("/api/v1/m/crm/opportunities", new
        {
            legalEntityId = entity,
            title = "Deal " + Guid.NewGuid().ToString("N")[..4],
            customerName = "Acme Corp",
            stage = 1,
            probabilityPercent = 20,
            expectedRevenue = 50000m
        });
        Assert.Equal(HttpStatusCode.Created, oppRes.StatusCode);
        var oppId = (string)(await Json(oppRes))["id"]!;

        var close1 = await c.PostAsJsonAsync($"/api/v1/m/crm/opportunities/{oppId}/close", new
        {
            id = Guid.Parse(oppId),
            isWon = true,
            actualRevenue = 50000m,
            closeReason = "Won deal"
        });
        Assert.Equal(HttpStatusCode.OK, close1.StatusCode);

        // Close again -> 409 Conflict
        var close2 = await c.PostAsJsonAsync($"/api/v1/m/crm/opportunities/{oppId}/close", new
        {
            id = Guid.Parse(oppId),
            isWon = true,
            actualRevenue = 50000m,
            closeReason = "Won deal"
        });
        Assert.Equal(HttpStatusCode.Conflict, close2.StatusCode);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> LoggedErrors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                LoggedErrors.Add(formatter(state, exception));
            }
        }
    }
}
