using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

public class PurchasingFlowTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pur = "/api/v1/m/purchasing";
    private const string Fin = "/api/v1/finance";
    private const string Inv = "/api/v1/m/inventory";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record PurchasingEnv(
        HttpClient C,
        string EntityId,
        string VendorPartyId,
        string WarehouseId,
        string ItemId,
        string ItemSku);

    private async Task<PurchasingEnv> SetupPurchasingEnvAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/purchasing/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);

        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "PurFlowLE " + Guid.NewGuid().ToString("N")[..6],
            countryCode = "AE",
            baseCurrency = "AED",
            isTaxRegistered = false
        }), HttpStatusCode.Created))["id"]!;

        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new
        {
            name = "Vendor " + Guid.NewGuid().ToString("N")[..6],
            kind = "Company",
            roles = "Vendor",
            paymentTermsDays = 30
        }), HttpStatusCode.Created))["id"]!;

        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new
        {
            code = "WH-" + Guid.NewGuid().ToString("N")[..4],
            name = "Purchasing Flow Warehouse",
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        var sku = "RAW-" + Guid.NewGuid().ToString("N")[..6];
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku,
            name = "Raw Component",
            type = "product",
            uom = "pcs",
            purchasePrice = 80m,
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        return new PurchasingEnv(c, entity, vendor, wh, item, sku);
    }

    [Fact]
    public async Task Full_purchasing_flow_requisition_to_po_to_receipt_to_bill()
    {
        var env = await SetupPurchasingEnvAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Create a purchase requisition
        var reqRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/requisitions", new
        {
            legalEntityId = env.EntityId,
            requesterName = "Alice Requester",
            department = "Operations",
            requisitionDate = today.ToString("yyyy-MM-dd"),
            requiredDate = today.AddDays(14).ToString("yyyy-MM-dd"),
            reason = "Monthly restocking",
            lines = new[]
            {
                new
                {
                    itemId = env.ItemId,
                    itemCode = env.ItemSku,
                    description = "Raw Component",
                    quantity = 10m,
                    unitOfMeasure = "pcs",
                    estimatedUnitPrice = 80m
                }
            }
        }), HttpStatusCode.Created);

        var reqId = (string)reqRes["id"]!;
        Assert.Equal("draft", (string)reqRes["status"]!, ignoreCase: true);
        Assert.Equal(800m, (decimal)reqRes["estimatedTotal"]!);

        // 2. Approve the requisition
        await Ok(await env.C.PutAsJsonAsync($"{Pur}/requisitions/{reqId}/status", new { status = "Approved" }));

        // 3. Convert requisition to purchase order
        var poRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/requisitions/{reqId}/convert-to-order", new
        {
            vendorPartyId = env.VendorPartyId,
            vendorName = "Vendor Corp"
        }));

        var orderId = (string)poRes["id"]!;
        var orderNum = (string)poRes["number"]!;
        Assert.Equal("draft", (string)poRes["status"]!, ignoreCase: true);
        Assert.Equal(reqId, (string)poRes["requisitionId"]!);
        Assert.Equal(800m, (decimal)poRes["totalAmount"]!);

        // Verify requisition status changed to Ordered and points to PO
        var reqCheck = await Ok(await env.C.GetAsync($"{Pur}/requisitions/{reqId}"));
        Assert.Equal("ordered", (string)reqCheck["status"]!, ignoreCase: true);
        Assert.Equal(orderId, (string)reqCheck["convertedPurchaseOrderId"]!);

        // 4. Approve the purchase order
        await Ok(await env.C.PutAsJsonAsync($"{Pur}/orders/{orderId}/status", new { status = "Approved" }));

        // 5. Create goods receipt
        var poLineId = (string)poRes["lines"]!.AsArray()[0]!["id"]!;
        var grnRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = env.EntityId,
            purchaseOrderId = orderId,
            receivedBy = "Warehouse Receiver",
            receiptDate = today.ToString("yyyy-MM-dd"),
            deliveryNoteNumber = "DN-12345",
            notes = "All goods received in good order",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = env.ItemId,
                    itemCode = env.ItemSku,
                    description = "Raw Component",
                    quantityReceived = 10m,
                    unitOfMeasure = "pcs"
                }
            }
        }), HttpStatusCode.Created);

        var grnId = (string)grnRes["id"]!;
        var grnNum = (string)grnRes["number"]!;

        // Verify PO status updated to Received
        var orderAfterGrn = await Ok(await env.C.GetAsync($"{Pur}/orders/{orderId}"));
        Assert.Equal("received", (string)orderAfterGrn["status"]!, ignoreCase: true);
        var poLineCheck = orderAfterGrn["lines"]!.AsArray()[0]!;
        Assert.Equal(10m, (decimal)poLineCheck["receivedQuantity"]!);

        // Assert stock movement: stock receive movement posted with matching reference
        var movementsRes = (await Ok(await env.C.GetAsync($"{Inv}/movements?pageSize=100&filter=itemId:eq:{env.ItemId}"))).AsArray();
        Assert.Contains(movementsRes, m => (string?)m!["reference"] == grnNum && string.Equals((string?)m!["kind"], "Receipt", StringComparison.OrdinalIgnoreCase) && (decimal)m!["quantity"]! == 10m);

        // 6. Convert purchase order to bill
        var billRes = await Ok(await env.C.PostAsync($"{Pur}/orders/{orderId}/convert-to-bill", null));
        var billId = (string)billRes["id"]!;
        Assert.Equal(800m, (decimal)billRes["total"]!);

        // Verify PO status changed to Billed and points to bill
        var orderAfterBill = await Ok(await env.C.GetAsync($"{Pur}/orders/{orderId}"));
        Assert.Equal("billed", (string)orderAfterBill["status"]!, ignoreCase: true);
        Assert.Equal(billId, (string)orderAfterBill["invoicedBillId"]!);

        // Verify bill in Finance
        var finBill = await Ok(await env.C.GetAsync($"{Fin}/bills/{billId}"));
        Assert.Equal(billId, (string)finBill["id"]!);
        Assert.Equal(env.VendorPartyId, (string)finBill["partyId"]!);
        Assert.Equal(800m, (decimal)finBill["total"]!);
    }

    [Fact]
    public async Task Tenant_isolation_second_workspace_cannot_read_or_access_purchasing_documents()
    {
        var env = await SetupPurchasingEnvAsync();

        // Create requisition & order in workspace A
        var reqRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/requisitions", new
        {
            legalEntityId = env.EntityId,
            requesterName = "Tenant Isolation Requester",
            requisitionDate = "2026-06-01",
            lines = new[] { new { itemCode = "ISO-PUR-01", description = "Test line", quantity = 1m, estimatedUnitPrice = 50m } }
        }), HttpStatusCode.Created);
        var reqId = (string)reqRes["id"]!;

        var orderRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = env.EntityId,
            vendorPartyId = env.VendorPartyId,
            vendorName = "Tenant Isolation Vendor",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "ISO-PUR-01", description = "Test line", quantity = 1m, unitPrice = 50m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        // Sign in to workspace B (demo-re)
        var (cB0, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await cB0.PostAsync("/api/v1/modules/purchasing/enable", null);
        var (cB, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");

        // Workspace B cannot fetch documents created in workspace A
        var getReq = await cB.GetAsync($"{Pur}/requisitions/{reqId}");
        Assert.Equal(HttpStatusCode.NotFound, getReq.StatusCode);

        var getOrder = await cB.GetAsync($"{Pur}/orders/{orderId}");
        Assert.Equal(HttpStatusCode.NotFound, getOrder.StatusCode);

        // Workspace B lists do not contain workspace A documents
        var reqsList = (await Ok(await cB.GetAsync($"{Pur}/requisitions?pageSize=100"))).AsArray();
        Assert.DoesNotContain(reqsList, r => (string?)r!["id"] == reqId);

        var ordersList = (await Ok(await cB.GetAsync($"{Pur}/orders?pageSize=100"))).AsArray();
        Assert.DoesNotContain(ordersList, o => (string?)o!["id"] == orderId);
    }

    [Fact]
    public async Task Permission_denial_read_only_user_cannot_create_or_mutate_purchasing_documents()
    {
        var env = await SetupPurchasingEnvAsync();

        // Create an existing requisition and order using admin client
        var reqRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/requisitions", new
        {
            legalEntityId = env.EntityId,
            requesterName = "Perm Test Requester",
            requisitionDate = "2026-06-01",
            lines = new[] { new { itemCode = "PERM-PUR-01", description = "Perm line", quantity = 1m, estimatedUnitPrice = 50m } }
        }), HttpStatusCode.Created);
        var reqId = (string)reqRes["id"]!;

        var orderRes = await Ok(await env.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = env.EntityId,
            vendorPartyId = env.VendorPartyId,
            vendorName = "Perm Test Vendor",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "PERM-PUR-01", description = "Perm line", quantity = 1m, unitPrice = 50m } }
        }), HttpStatusCode.Created);
        var orderId = (string)orderRes["id"]!;

        // Admin creates a role with read-only permissions
        var roleRes = await Ok(await env.C.PostAsJsonAsync("/api/v1/roles", new
        {
            name = "Purchasing Read-Only " + Guid.NewGuid().ToString("N")[..6],
            description = "Read-only purchasing access",
            permissions = new[] { "purchasing.orders.read", "purchasing.requisitions.read", "purchasing.receipts.read" }
        }), HttpStatusCode.Created);
        var roleId = (string)roleRes["id"]!;

        var email = "purro" + Guid.NewGuid().ToString("N")[..6] + "@demo.local";
        await Ok(await env.C.PostAsJsonAsync("/api/v1/users", new
        {
            email,
            fullName = "Purchasing Read Only User",
            temporaryPassword = "Temp#Passw0rd1",
            roleIds = new[] { roleId }
        }), HttpStatusCode.Created);

        var (roClient, _) = await f.SignInAsync(email, "Temp#Passw0rd1");

        // Can read lists and details
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Pur}/requisitions")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Pur}/requisitions/{reqId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Pur}/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Pur}/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await roClient.GetAsync($"{Pur}/receipts")).StatusCode);

        // Cannot create or mutate requisitions
        var createReq = await roClient.PostAsJsonAsync($"{Pur}/requisitions", new
        {
            legalEntityId = env.EntityId,
            requesterName = "Denied Requester",
            requisitionDate = "2026-06-01",
            lines = new[] { new { itemCode = "X", description = "X", quantity = 1m, estimatedUnitPrice = 10m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, createReq.StatusCode);

        var updateReqStatus = await roClient.PutAsJsonAsync($"{Pur}/requisitions/{reqId}/status", new { status = "Approved" });
        Assert.Equal(HttpStatusCode.Forbidden, updateReqStatus.StatusCode);

        var convertReq = await roClient.PostAsJsonAsync($"{Pur}/requisitions/{reqId}/convert-to-order", new
        {
            vendorPartyId = env.VendorPartyId,
            vendorName = "Denied Vendor"
        });
        Assert.Equal(HttpStatusCode.Forbidden, convertReq.StatusCode);

        // Cannot create or mutate orders
        var createOrder = await roClient.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = env.EntityId,
            vendorPartyId = env.VendorPartyId,
            vendorName = "Denied Vendor",
            orderDate = "2026-06-01",
            currency = "AED",
            lines = new[] { new { itemCode = "X", description = "X", quantity = 1m, unitPrice = 10m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, createOrder.StatusCode);

        var updateOrderStatus = await roClient.PutAsJsonAsync($"{Pur}/orders/{orderId}/status", new { status = "Approved" });
        Assert.Equal(HttpStatusCode.Forbidden, updateOrderStatus.StatusCode);

        var createReceipt = await roClient.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = env.EntityId,
            purchaseOrderId = orderId,
            receivedBy = "Denied User",
            receiptDate = "2026-06-05",
            lines = new[] { new { purchaseOrderLineId = Guid.NewGuid(), itemCode = "X", description = "X", quantityReceived = 1m, unitOfMeasure = "EA" } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, createReceipt.StatusCode);

        var convertOrder = await roClient.PostAsync($"{Pur}/orders/{orderId}/convert-to-bill", null);
        Assert.Equal(HttpStatusCode.Forbidden, convertOrder.StatusCode);
    }
}
