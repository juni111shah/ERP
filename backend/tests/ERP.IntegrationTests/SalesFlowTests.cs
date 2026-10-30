using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

public class SalesFlowTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Sales = "/api/v1/m/sales";
    private const string Fin = "/api/v1/finance";
    private const string Inv = "/api/v1/m/inventory";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record SalesEnv(
        HttpClient C,
        string EntityId,
        string CustomerPartyId,
        string WarehouseId,
        string ItemId,
        string ItemSku,
        string TaxCodeId);

    private async Task<SalesEnv> SetupSalesEnvAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/sales/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "SalesFlowLE " + Guid.NewGuid().ToString("N")[..6],
            countryCode = "AE",
            baseCurrency = "AED",
            isTaxRegistered = true
        }), HttpStatusCode.Created))["id"]!;

        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new
        {
            name = "Customer " + Guid.NewGuid().ToString("N")[..6],
            kind = "Company",
            roles = "Customer",
            paymentTermsDays = 15
        }), HttpStatusCode.Created))["id"]!;

        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new
        {
            code = "WH-" + Guid.NewGuid().ToString("N")[..4],
            name = "Sales Flow Warehouse",
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        var sku = "PROD-" + Guid.NewGuid().ToString("N")[..6];
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku,
            name = "Finished Good",
            type = "product",
            uom = "EA",
            purchasePrice = 50m,
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        var tax5 = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new
        {
            code = "TX" + Guid.NewGuid().ToString("N")[..6],
            name = "VAT 5%",
            kind = "vat",
            rate = 5,
            isDefault = true,
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray();
        var offsetAccount = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;

        // Receive initial stock into the warehouse so there's enough stock to issue upon fulfillment
        await Ok(await c.PostAsJsonAsync($"{Inv}/stock/receive", new
        {
            warehouseId = wh,
            itemId = item,
            quantity = 50m,
            unitCost = 50m,
            date = "2026-05-01",
            offsetAccountId = offsetAccount,
            reference = "INIT-STOCK",
            memo = "Initial stock"
        }), HttpStatusCode.Created);

        return new SalesEnv(c, entity, customer, wh, item, sku, tax5);
    }

    [Fact]
    public async Task Full_sales_flow_quote_to_order_to_fulfilment_to_invoice()
    {
        var env = await SetupSalesEnvAsync();

        // 1. Create a sales quote
        var quoteRes = await Ok(await env.C.PostAsJsonAsync($"{Sales}/quotes", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Flow Buyer",
            quoteDate = "2026-06-01",
            validUntil = "2026-06-30",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemId = env.ItemId,
                    itemCode = env.ItemSku,
                    description = "Finished Good Unit",
                    quantity = 5m,
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 5m
                }
            }
        }), HttpStatusCode.Created);

        var quoteId = (string)quoteRes["id"]!;
        Assert.Equal("draft", (string)quoteRes["status"]!, ignoreCase: true);
        Assert.Equal(500m, (decimal)quoteRes["subTotal"]!);
        Assert.Equal(25m, (decimal)quoteRes["taxTotal"]!);
        Assert.Equal(525m, (decimal)quoteRes["totalAmount"]!);

        // 2. Accept the quote
        await Ok(await env.C.PutAsJsonAsync($"{Sales}/quotes/{quoteId}/status", new { status = "Accepted" }));

        // 3. Convert quote to sales order (accepted quote converts to confirmed order)
        var orderRes = await Ok(await env.C.PostAsync($"{Sales}/quotes/{quoteId}/convert-to-order", null));
        var orderId = (string)orderRes["id"]!;
        var orderNum = (string)orderRes["number"]!;
        Assert.Equal("confirmed", (string)orderRes["status"]!, ignoreCase: true);
        Assert.Equal(quoteId, (string)orderRes["quoteId"]!);
        Assert.Equal(525m, (decimal)orderRes["totalAmount"]!);

        // Verify quote status changed to Converted and points to sales order
        var quoteCheck = await Ok(await env.C.GetAsync($"{Sales}/quotes/{quoteId}"));
        Assert.Equal("converted", (string)quoteCheck["status"]!, ignoreCase: true);
        Assert.Equal(orderId, (string)quoteCheck["convertedSalesOrderId"]!);

        // 5. Fulfill the sales order
        var orderLineId = (string)orderRes["lines"]!.AsArray()[0]!["id"]!;
        var fulRes = await Ok(await env.C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId = orderLineId, deliveredQuantity = 5m } }
        }));
        Assert.Equal("delivered", (string)fulRes["status"]!, ignoreCase: true);
        var line = fulRes["lines"]!.AsArray()[0]!;
        Assert.Equal(5m, (decimal)line["deliveredQuantity"]!);

        // Assert stock movement: stock issue movement posted with matching reference
        var movementsRes = (await Ok(await env.C.GetAsync($"{Inv}/movements?pageSize=100&filter=itemId:eq:{env.ItemId}"))).AsArray();
        Assert.Contains(movementsRes, m => (string?)m!["reference"] == orderNum && string.Equals((string?)m!["kind"], "Issue", StringComparison.OrdinalIgnoreCase) && (decimal)m!["quantity"]! == -5m);

        // 6. Convert order to invoice
        var invoiceRes = await Ok(await env.C.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null));
        var invoiceId = (string)invoiceRes["id"]!;
        Assert.Equal("salesorder", (string)invoiceRes["sourceType"]!);
        Assert.Equal(orderId, (string)invoiceRes["sourceId"]!);
        Assert.Equal(525m, (decimal)invoiceRes["total"]!);

        // Verify sales order status changed to Invoiced and points to invoice
        var orderCheck = await Ok(await env.C.GetAsync($"{Sales}/orders/{orderId}"));
        Assert.Equal("invoiced", (string)orderCheck["status"]!, ignoreCase: true);
        Assert.Equal(invoiceId, (string)orderCheck["invoicedInvoiceId"]!);

        // Verify invoice is linked in Finance
        var finInvoice = await Ok(await env.C.GetAsync($"{Fin}/invoices/{invoiceId}"));
        Assert.Equal(invoiceId, (string)finInvoice["id"]!);
        Assert.Equal(env.CustomerPartyId, (string)finInvoice["partyId"]!);
        Assert.Equal(525m, (decimal)finInvoice["total"]!);
    }

    [Fact]
    public async Task Tenant_isolation_second_workspace_cannot_read_or_access_sales_documents()
    {
        var env = await SetupSalesEnvAsync();

        // Create quote & order in workspace A
        var quoteRes = await Ok(await env.C.PostAsJsonAsync($"{Sales}/quotes", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Tenant Isolation Buyer",
            quoteDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ISO-01", description = "Test line", quantity = 1m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var quoteId = (string)quoteRes["id"]!;

        var orderRes = await Ok(await env.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Tenant Isolation Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ISO-01", description = "Test line", quantity = 1m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        // Sign in to workspace B (demo-re)
        var (cB0, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await cB0.PostAsync("/api/v1/modules/sales/enable", null);
        var (cB, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");

        // Workspace B cannot fetch documents created in workspace A
        var getQuote = await cB.GetAsync($"{Sales}/quotes/{quoteId}");
        Assert.Equal(HttpStatusCode.NotFound, getQuote.StatusCode);

        var getOrder = await cB.GetAsync($"{Sales}/orders/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, getOrder.StatusCode);

        // Workspace B lists do not contain workspace A documents
        var quotesList = (await Ok(await cB.GetAsync($"{Sales}/quotes?pageSize=100"))).AsArray();
        Assert.DoesNotContain(quotesList, q => (string?)q!["id"] == quoteId);

        var ordersList = (await Ok(await cB.GetAsync($"{Sales}/orders?pageSize=100"))).AsArray();
        Assert.DoesNotContain(ordersList, o => (string?)o!["id"] == orderId);
    }

    [Fact]
    public async Task Permission_denial_read_only_user_cannot_create_or_mutate_sales_documents()
    {
        var env = await SetupSalesEnvAsync();

        // Create an existing quote and order using admin client
        var quoteRes = await Ok(await env.C.PostAsJsonAsync($"{Sales}/quotes", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Perm Test Buyer",
            quoteDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "PERM-01", description = "Perm line", quantity = 1m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var quoteId = (string)quoteRes["id"]!;

        var orderRes = await Ok(await env.C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Perm Test Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "PERM-01", description = "Perm line", quantity = 1m, unitPrice = 100m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        // Admin creates a role with read-only permissions
        var roleRes = await Ok(await env.C.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Sales Read-Only " + Guid.NewGuid().ToString("N")[..6],
            description = "Read-only sales access",
            permissions = new[] { "sales.quotes.read", "sales.orders.read" }
        }), HttpStatusCode.Created);
        var roleId = (string)roleRes["id"]!;

        var email = "salesro" + Guid.NewGuid().ToString("N")[..6] + "@demo.local";
        await Ok(await env.C.PostAsJsonAsync("/api/v1/users", new
        {
            email,
            fullName = "Sales Read Only User",
            temporaryPassword = "Temp#Passw0rd1",
            roleIds = new[] { roleId }
        }), HttpStatusCode.Created);

        var (roClient, _) = await f.SignInAsync(email, "Temp#Passw0rd1");

        // Can read lists and details
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Sales}/quotes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Sales}/quotes/{quoteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Sales}/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Sales}/orders/{orderId}")).StatusCode);

        // Cannot create or mutate quotes
        var createQuote = await roClient.PostAsJsonAsync($"{Sales}/quotes", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Denied Buyer",
            quoteDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "X", description = "X", quantity = 1m, unitPrice = 10m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, createQuote.StatusCode);

        var updateQuoteStatus = await roClient.PutAsJsonAsync($"{Sales}/quotes/{quoteId}/status", new { status = "Accepted" });
        Assert.Equal(HttpStatusCode.Forbidden, updateQuoteStatus.StatusCode);

        var convertQuote = await roClient.PostAsync($"{Sales}/quotes/{quoteId}/convert-to-order", null);
        Assert.Equal(HttpStatusCode.Forbidden, convertQuote.StatusCode);

        // Cannot create or mutate orders
        var createOrder = await roClient.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = env.EntityId,
            customerPartyId = env.CustomerPartyId,
            customerName = "Denied Buyer",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "X", description = "X", quantity = 1m, unitPrice = 10m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, createOrder.StatusCode);

        var updateFulfillment = await roClient.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new
        {
            lines = new[] { new { lineId = Guid.NewGuid(), deliveredQuantity = 1m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, updateFulfillment.StatusCode);

        var convertOrder = await roClient.PostAsync($"{Sales}/orders/{orderId}/convert-to-invoice", null);
        Assert.Equal(HttpStatusCode.Forbidden, convertOrder.StatusCode);
    }
}
