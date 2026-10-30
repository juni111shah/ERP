using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Domain.Finance;
using ERP.Modules.Pos;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

public class PosOrderTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Json(HttpResponseMessage res) =>
        ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    private async Task<(HttpClient AdminClient, string EntityId, string RegisterId, string SessionId)> SetupPosAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);

        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var me = await ApiFactory.Json(await admin.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;

        var regRes = await admin.PostAsJsonAsync("/api/v1/m/pos/registers", new
        {
            legalEntityId = entity,
            code = "REG-" + Guid.NewGuid().ToString("N")[..4],
            name = "Main Counter Register"
        });
        regRes.EnsureSuccessStatusCode();
        var regId = (string)(await Json(regRes))["id"]!;

        var openRes = await admin.PostAsJsonAsync("/api/v1/m/pos/sessions/open", new
        {
            legalEntityId = entity,
            registerId = regId,
            cashierUserId = Guid.NewGuid(),
            cashierName = "Cashier 1",
            openingCash = 100m
        });
        openRes.EnsureSuccessStatusCode();
        var sessionId = (string)(await Json(openRes))["id"]!;

        return (admin, entity, regId, sessionId);
    }

    [Fact]
    public async Task Lower_unit_price_returns_400_unless_override_permission_is_held()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // 1. Create an item priced at 50.00
        var itemSku = "ITEM-" + Guid.NewGuid().ToString("N")[..6];
        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: itemSku,
            Name: "Specialty Tea",
            Type: ItemType.Product,
            Uom: "ea",
            SalesPrice: 50.00m,
            PurchasePrice: 20.00m,
            TaxCodeId: null));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        // 2. Create a cashier role with pos.orders.create but WITHOUT pos.orders.override-price
        var cashierRoleName = "CashierStandard_" + Guid.NewGuid().ToString("N")[..6];
        var cashierRoleRes = await admin.PostAsJsonAsync("/api/v1/roles", new RoleInput(
            cashierRoleName,
            "Standard Cashier without price override",
            [Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.RegistersRead, Permissions.SessionsRead]));
        cashierRoleRes.EnsureSuccessStatusCode();
        var cashierRoleId = Guid.Parse((string)(await Json(cashierRoleRes))["id"]!);

        var cashierEmail = $"cashier_{Guid.NewGuid():N}@demo.local";
        var userRes = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = cashierEmail,
            fullName = "Standard Cashier",
            temporaryPassword = "Temp#Passw0rd",
            roleIds = new[] { cashierRoleId }
        });
        userRes.EnsureSuccessStatusCode();
        var (cashierClient, _) = await f.SignInAsync(cashierEmail, "Temp#Passw0rd");

        // 3. Cashier attempts to sell item for 40.00 (lower than 50.00) -> 400 BadRequest
        var underPriceOrder = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Customer 1",
            [
                new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 40.00m, DiscountPercent: 0)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 40.00m, 40.00m, 0m, null)
            ]);

        var failRes = await cashierClient.PostAsJsonAsync("/api/v1/m/pos/orders", underPriceOrder);
        Assert.Equal(HttpStatusCode.BadRequest, failRes.StatusCode);

        // 4. Admin has all permissions including pos.orders.override-price -> posting 40.00 succeeds
        var okRes = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", underPriceOrder);
        Assert.Equal(HttpStatusCode.Created, okRes.StatusCode);
        var createdOrder = await Json(okRes);
        Assert.Equal(40.00m, (decimal)createdOrder["totalAmount"]!);
    }

    [Fact]
    public async Task Posting_cashierUserId_of_another_user_is_ignored_and_stored_cashier_equals_caller()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // Admin's info
        var meRes = await admin.GetAsync("/api/v1/me");
        var me = await ApiFactory.Json(meRes);
        var callerUserId = (string)me["user"]!["id"]!;
        var callerEmail = (string)me["user"]!["email"]!;

        var anotherUserGuid = Guid.NewGuid();
        var cmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Customer 2",
            [
                new PosOrderLineInput(null, ItemCode: "ADHOC-1", Description: "Adhoc snack", Quantity: 1, UnitPrice: 15.00m, DiscountPercent: 0, TaxRate: 0)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 15.00m, 20.00m, 5.00m, null)
            ],
            Notes: "Testing cashier identity",
            CustomFields: null,
            CashierUserId: anotherUserGuid,
            CashierName: "Impostor Cashier");

        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", cmd);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var order = await Json(res);
        Assert.Equal(callerUserId, (string)order["cashierUserId"]!);
        Assert.Equal(callerEmail, (string)order["cashierName"]!);
    }

    [Fact]
    public async Task Under_payment_returns_400()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // Order total is 25.00, client tenders/pays only 20.00
        var cmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Walk-in",
            [
                new PosOrderLineInput(null, ItemCode: "ITEM-A", Description: "Product A", Quantity: 1, UnitPrice: 25.00m, DiscountPercent: 0, TaxRate: 0)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 20.00m, 20.00m, 0m, null)
            ]);

        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", cmd);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Discount_above_configured_maximum_returns_400()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // 1. Configure pos.maxDiscountPercent to 20
        var settingRes = await admin.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object>
        {
            ["pos.maxDiscountPercent"] = 20
        });
        Assert.Equal(HttpStatusCode.NoContent, settingRes.StatusCode);

        // 2. Line with discount = 25% -> 400 BadRequest
        var invalidCmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Walk-in",
            [
                new PosOrderLineInput(null, ItemCode: "ITEM-B", Description: "Product B", Quantity: 1, UnitPrice: 100.00m, DiscountPercent: 25m, TaxRate: 0)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 75.00m, 75.00m, 0m, null)
            ]);

        var failRes = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", invalidCmd);
        Assert.Equal(HttpStatusCode.BadRequest, failRes.StatusCode);

        // 3. Line with discount = 20% -> 201 Created
        var validCmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Walk-in",
            [
                new PosOrderLineInput(null, ItemCode: "ITEM-B", Description: "Product B", Quantity: 1, UnitPrice: 100.00m, DiscountPercent: 20m, TaxRate: 0)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 80.00m, 80.00m, 0m, null)
            ]);

        var okRes = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", validCmd);
        Assert.Equal(HttpStatusCode.Created, okRes.StatusCode);
        var order = await Json(okRes);
        Assert.Equal(80.00m, (decimal)order["totalAmount"]!);
    }

    [Fact]
    public async Task Pos_order_server_pricing_and_tax_code_resolution()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // Create tax code at 5%
        var taxCode = "TAX-" + Guid.NewGuid().ToString("N")[..4];
        var taxRes = await admin.PostAsJsonAsync("/api/v1/finance/tax-codes", new TaxCodeInput(
            Code: taxCode,
            Name: "5% VAT",
            Kind: TaxKind.Vat,
            Rate: 5m));
        taxRes.EnsureSuccessStatusCode();
        var taxCodeId = Guid.Parse((string)(await Json(taxRes))["id"]!);

        // Create item priced at 100 with 5% tax code
        var itemSku = "ITEM-" + Guid.NewGuid().ToString("N")[..6];
        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: itemSku,
            Name: "Server Priced Item",
            Type: ItemType.Product,
            Uom: "ea",
            SalesPrice: 100.00m,
            PurchasePrice: 50.00m,
            TaxCodeId: taxCodeId));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        // Terminal sends only itemId and quantity: 2 items @ 100 = 200 subtotal, 10 tax (5%), 210 total
        var orderCmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Server Priced Customer",
            [
                new PosOrderLineInput(itemId, Quantity: 2)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 210.00m, 250.00m, 40.00m, null)
            ]);

        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", orderCmd);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var order = await Json(res);
        Assert.Equal(200.00m, (decimal)order["subTotal"]!);
        Assert.Equal(10.00m, (decimal)order["taxTotal"]!);
        Assert.Equal(210.00m, (decimal)order["totalAmount"]!);
        Assert.Equal("Paid", (string)order["paymentStatus"]!, ignoreCase: true);

        var line = order["lines"]![0]!;
        Assert.Equal(100.00m, (decimal)line["unitPrice"]!);
        Assert.Equal(5.00m, (decimal)line["taxRate"]!);
        Assert.Equal(210.00m, (decimal)line["lineTotal"]!);
    }

    [Fact]
    public async Task Void_in_closed_session_returns_409_and_changes_nothing()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        var itemSku = "ITEM-" + Guid.NewGuid().ToString("N")[..6];
        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: itemSku,
            Name: "Sample Item",
            Type: ItemType.Product,
            Uom: "ea",
            SalesPrice: 50.00m,
            PurchasePrice: 20.00m,
            TaxCodeId: null));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        var orderCmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Customer 1",
            [
                new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 50.00m)
            ],
            [
                new PosPaymentInput(PosPaymentMethod.Cash, 50.00m, 50.00m, 0m, null)
            ]);

        var orderRes = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", orderCmd);
        orderRes.EnsureSuccessStatusCode();
        var orderId = (string)(await Json(orderRes))["id"]!;

        // Close session
        var closeRes = await admin.PostAsJsonAsync($"/api/v1/m/pos/sessions/{sessionId}/close", new
        {
            actualCash = 150m,
            closedByUserId = Guid.NewGuid(),
            notes = "End of shift"
        });
        closeRes.EnsureSuccessStatusCode();

        // Attempt void in closed session -> 409 Conflict
        var voidRes = await admin.PostAsJsonAsync($"/api/v1/m/pos/orders/{orderId}/void", new { reason = "Mistake" });
        Assert.Equal(HttpStatusCode.Conflict, voidRes.StatusCode);

        // Verify order is still Completed (not Voided)
        var checkOrder = await admin.GetAsync($"/api/v1/m/pos/orders/{orderId}");
        var orderJson = await Json(checkOrder);
        Assert.Equal("Completed", (string)orderJson["status"]!, ignoreCase: true);

        // Verify session totals are unchanged
        var sessionsList = await Json(await admin.GetAsync("/api/v1/m/pos/sessions"));
        var sessionSummary = sessionsList.AsArray().First(s => (string)s!["id"]! == sessionId);
        Assert.Equal(50m, (decimal)sessionSummary!["totalSales"]!);
        Assert.Equal(0m, (decimal)sessionSummary!["totalReturns"]!);
        Assert.Equal(150m, (decimal)sessionSummary!["expectedCash"]!);
    }

    [Fact]
    public async Task After_cash_void_expected_cash_equals_opening_cash_plus_remaining_cash_sales()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync(); // opening cash = 100

        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name: "Cash Item",
            Type: ItemType.Product,
            Uom: "ea",
            SalesPrice: 50.00m,
            PurchasePrice: 20.00m,
            TaxCodeId: null));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        // Order 1: 50 cash (tendered 60, change 10) -> cashIn = 50
        var order1Res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Customer 1",
            [new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 50.00m)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 50.00m, 60.00m, 10.00m, null)]));
        order1Res.EnsureSuccessStatusCode();
        var order1Id = (string)(await Json(order1Res))["id"]!;

        // Order 2: 30 cash (tendered 30, change 0) -> cashIn = 30
        var order2Res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Customer 2",
            [new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 30.00m)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 30.00m, 30.00m, 0m, null)]));
        order2Res.EnsureSuccessStatusCode();

        // Check session before void: Expected = 100 + 50 + 30 = 180, TotalSales = 80
        var sBefore = await Json(await admin.GetAsync($"/api/v1/m/pos/sessions/current?registerId={regId}"));
        Assert.Equal(180m, (decimal)sBefore["expectedCash"]!);
        Assert.Equal(80m, (decimal)sBefore["totalSales"]!);

        // Void Order 1
        var voidRes = await admin.PostAsJsonAsync($"/api/v1/m/pos/orders/{order1Id}/void", new { reason = "Wrong order" });
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var voidedOrder = await Json(voidRes);
        Assert.Equal("Voided", (string)voidedOrder["status"]!, ignoreCase: true);

        // Check session after void: Expected = 100 + 30 = 130, TotalSales = 30, TotalReturns = 50
        var sAfter = await Json(await admin.GetAsync($"/api/v1/m/pos/sessions/current?registerId={regId}"));
        Assert.Equal(130m, (decimal)sAfter["expectedCash"]!);
        Assert.Equal(30m, (decimal)sAfter["totalSales"]!);
        Assert.Equal(50m, (decimal)sAfter["totalReturns"]!);
    }

    [Fact]
    public async Task Inventory_value_after_sale_and_void_equals_value_before_sale()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);

        var (admin, entity, regId, sessionId) = await SetupPosAsync();

        // Create warehouse
        var whRes = await admin.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new
        {
            code = "WH-" + Guid.NewGuid().ToString("N")[..4],
            name = "POS Warehouse",
            legalEntityId = entity
        });
        whRes.EnsureSuccessStatusCode();
        var whId = (string)(await Json(whRes))["id"]!;

        // Update register to use this warehouse
        await admin.PutAsJsonAsync($"/api/v1/m/pos/registers/{regId}", new UpdateRegisterCommand(
            Id: Guid.Parse(regId),
            Name: "Register with WH",
            WarehouseId: Guid.Parse(whId),
            DefaultCashAccountId: null,
            IsActive: true,
            ReceiptHeader: null,
            ReceiptFooter: null,
            CustomFields: null));

        // Create item with SalesPrice = 50, PurchasePrice = 10 (different from actual stock cost)
        var itemSku = "STK-" + Guid.NewGuid().ToString("N")[..6];
        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: itemSku,
            Name: "Tracked Product",
            Type: ItemType.Product,
            Uom: "pcs",
            SalesPrice: 50.00m,
            PurchasePrice: 10.00m,
            TaxCodeId: null));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        // Receive 10 units at cost 15.00 -> total value = 150.00
        var accounts = (await Json(await admin.GetAsync("/api/v1/finance/accounts?pageSize=300"))).AsArray();
        var offsetAccount = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;

        var rcvRes = await admin.PostAsJsonAsync("/api/v1/m/inventory/stock/receive", new
        {
            warehouseId = whId,
            itemId = itemId.ToString(),
            quantity = 10m,
            unitCost = 15.00m,
            date = "2026-03-01",
            offsetAccountId = offsetAccount
        });
        rcvRes.EnsureSuccessStatusCode();

        // Check stock value before sale: Qty = 10, TotalValue = 150.00
        var stockBefore = await Json(await admin.GetAsync($"/api/v1/m/inventory/stock?warehouseId={whId}&itemId={itemId}"));
        var rowBefore = stockBefore["rows"]!.AsArray().Single();
        var qtyBefore = (decimal)rowBefore!["quantity"]!;
        var valueBefore = (decimal)rowBefore!["value"]!;
        Assert.Equal(10m, qtyBefore);
        Assert.Equal(150m, valueBefore);

        // Place POS sale: sell 2 units for 50 each (total 100)
        var orderCmd = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Inventory Buyer",
            [new PosOrderLineInput(itemId, Quantity: 2m, UnitPrice: 50.00m)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 100.00m, 100.00m, 0m, null)]);
        var orderRes = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", orderCmd);
        orderRes.EnsureSuccessStatusCode();
        var orderId = (string)(await Json(orderRes))["id"]!;

        // Check stock after sale: Qty = 8, TotalValue = 120.00 (issued 2 @ 15 = 30)
        var stockMid = await Json(await admin.GetAsync($"/api/v1/m/inventory/stock?warehouseId={whId}&itemId={itemId}"));
        var rowMid = stockMid["rows"]!.AsArray().Single();
        Assert.Equal(8m, (decimal)rowMid!["quantity"]!);
        Assert.Equal(120m, (decimal)rowMid!["value"]!);

        // Void the POS order
        var voidRes = await admin.PostAsJsonAsync($"/api/v1/m/pos/orders/{orderId}/void", new { reason = "Void Sale" });
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);

        // Check stock after void: Qty = 10, TotalValue = 150.00 (restored at original 15 cost)
        var stockAfter = await Json(await admin.GetAsync($"/api/v1/m/inventory/stock?warehouseId={whId}&itemId={itemId}"));
        var rowAfter = stockAfter["rows"]!.AsArray().Single();
        Assert.Equal(qtyBefore, (decimal)rowAfter!["quantity"]!);
        Assert.Equal(valueBefore, (decimal)rowAfter!["value"]!);
    }

    [Fact]
    public async Task Two_concurrent_orders_in_one_session_produce_correct_totals()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync(); // opening cash = 100

        var itemRes = await admin.PostAsJsonAsync("/api/v1/items", new ItemInput(
            Sku: "ITEM-" + Guid.NewGuid().ToString("N")[..6],
            Name: "Concurrent Product",
            Type: ItemType.Product,
            Uom: "ea",
            SalesPrice: 100.00m,
            PurchasePrice: 50.00m,
            TaxCodeId: null));
        itemRes.EnsureSuccessStatusCode();
        var itemId = Guid.Parse((string)(await Json(itemRes))["id"]!);

        var cmd1 = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Buyer 1",
            [new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 60.00m)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 60.00m, 60.00m, 0m, null)]);

        var cmd2 = new CreatePosOrderCommand(
            Guid.Parse(entity),
            Guid.Parse(regId),
            Guid.Parse(sessionId),
            null,
            "Buyer 2",
            [new PosOrderLineInput(itemId, Quantity: 1, UnitPrice: 40.00m)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 40.00m, 40.00m, 0m, null)]);

        // Send two concurrent orders simultaneously
        var task1 = admin.PostAsJsonAsync("/api/v1/m/pos/orders", cmd1);
        var task2 = admin.PostAsJsonAsync("/api/v1/m/pos/orders", cmd2);

        var responses = await Task.WhenAll(task1, task2);
        Assert.Equal(HttpStatusCode.Created, responses[0].StatusCode);
        Assert.Equal(HttpStatusCode.Created, responses[1].StatusCode);

        // Session totals must be exact sum without lost updates
        var session = await Json(await admin.GetAsync($"/api/v1/m/pos/sessions/current?registerId={regId}"));
        Assert.Equal(100m, (decimal)session["totalSales"]!); // 60 + 40
        Assert.Equal(200m, (decimal)session["expectedCash"]!); // 100 opening + 60 + 40
    }

    private static CreatePosOrderCommand Adhoc(string entity, string regId, string sessionId, decimal price, PosPaymentInput[] pay, decimal? taxRate = 0m)
        => new(Guid.Parse(entity), Guid.Parse(regId), Guid.Parse(sessionId), null, "Customer",
            [new PosOrderLineInput(null, ItemCode: "ADHOC-" + Guid.NewGuid().ToString("N")[..4], Description: "Adhoc", Quantity: 1, UnitPrice: price, DiscountPercent: 0, TaxRate: taxRate)],
            [.. pay]);

    [Fact]
    public async Task Cash_amount_above_the_order_total_is_rejected()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();
        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 100m, [new PosPaymentInput(PosPaymentMethod.Cash, 150m, 150m, 0m, null)]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Cash_tendered_above_the_total_gives_change_and_raises_expected_cash_by_the_amount_applied()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync(); // opening 100
        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 100m, [new PosPaymentInput(PosPaymentMethod.Cash, 100m, 150m, 0m, null)]));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode); // created means the ledger entry balanced
        var order = await Json(res);
        Assert.Equal(50m, (decimal)order["payments"]![0]!["changeAmount"]!);
        var session = await Json(await admin.GetAsync($"/api/v1/m/pos/sessions/current?registerId={regId}"));
        Assert.Equal(200m, (decimal)session["expectedCash"]!);
    }

    [Fact]
    public async Task Two_card_lines_that_together_exceed_the_total_are_rejected()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();
        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 100m,
            [new PosPaymentInput(PosPaymentMethod.Card, 60m, 60m, 0m, null), new PosPaymentInput(PosPaymentMethod.Card, 60m, 60m, 0m, null)]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Exact_payment_still_succeeds()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();
        var res = await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 100m,
            [new PosPaymentInput(PosPaymentMethod.Card, 60m, 60m, 0m, null), new PosPaymentInput(PosPaymentMethod.Cash, 40m, 40m, 0m, null)]));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task Void_with_a_blank_reason_returns_400()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();
        var order = await Json(await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 20m, [new PosPaymentInput(PosPaymentMethod.Cash, 20m, 20m, 0m, null)])));
        var id = (string)order["id"]!;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/m/pos/orders/{id}/void", new { reason = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/m/pos/orders/{id}/void", new { reason = new string('x', 501) })).StatusCode);
    }

    [Fact]
    public async Task Free_text_lines_need_the_price_override_permission_and_a_valid_tax_rate()
    {
        var (admin, entity, regId, sessionId) = await SetupPosAsync();
        var roleRes = await admin.PostAsJsonAsync("/api/v1/roles", new RoleInput("CashierNoOverride_" + Guid.NewGuid().ToString("N")[..6], "No override",
            [Permissions.OrdersCreate, Permissions.OrdersRead, Permissions.RegistersRead, Permissions.SessionsRead]));
        roleRes.EnsureSuccessStatusCode();
        var email = $"cashier_{Guid.NewGuid():N}@demo.local";
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Cashier", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { Guid.Parse((string)(await Json(roleRes))["id"]!) } })).EnsureSuccessStatusCode();
        var (cashier, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        var pay = new[] { new PosPaymentInput(PosPaymentMethod.Cash, 25m, 25m, 0m, null) };

        Assert.Equal(HttpStatusCode.BadRequest, (await cashier.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 25m, pay))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 25m, pay))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 25m, pay, 101m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/m/pos/orders", Adhoc(entity, regId, sessionId, 25m, pay, -1m))).StatusCode);
    }
}
