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
}
