using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

public class PurchasingLedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Inv = "/api/v1/m/inventory";
    private const string Pur = "/api/v1/m/purchasing";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(
        HttpClient C,
        string Entity,
        string VendorPartyId,
        string WarehouseId,
        string ItemId,
        string ItemSku);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/purchasing/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new
        {
            name = "PurEnt " + Guid.NewGuid().ToString("N")[..6],
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
            name = "Purchasing Warehouse",
            legalEntityId = entity
        }), HttpStatusCode.Created))["id"]!;

        var sku = "RAW-" + Guid.NewGuid().ToString("N")[..6];
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new
        {
            sku,
            name = "Raw Material",
            type = "product",
            uom = "pcs",
            purchasePrice = 100m,
            isActive = true
        }), HttpStatusCode.Created))["id"]!;

        return new Ctx(c, entity, vendor, wh, item, sku);
    }

    private static async Task<decimal> BalanceAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    private static async Task<(decimal Qty, decimal TotalValue)> StockAsync(Ctx x)
    {
        var stockReport = await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={x.ItemId}"));
        var rows = stockReport["rows"]!.AsArray();
        if (rows.Count == 0) return (0m, 0m);
        var first = rows.First()!;
        return ((decimal)first["quantity"]!, (decimal)stockReport["totalValue"]!);
    }

    [Fact]
    public async Task Receive_then_bill_leaves_clearing_at_zero_and_ap_equal_to_bill_total()
    {
        var x = await SetupAsync();

        // 1. Create a Purchase Order for 10 units @ 100 AED = 1000 AED
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = x.Entity,
            vendorPartyId = x.VendorPartyId,
            vendorName = "Vendor Corp",
            orderDate = "2026-05-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantity = 10m,
                    unitOfMeasure = "pcs",
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 0m
                }
            }
        }), HttpStatusCode.Created);

        var poId = (string)po["id"]!;
        var poLineId = (string)po["lines"]!.AsArray()[0]!["id"]!;

        // Approve the PO
        await Ok(await x.C.PutAsJsonAsync($"{Pur}/orders/{poId}/status", new { status = "Approved" }));

        // 2. Create Goods Receipt for 10 units
        var grn = await Ok(await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-02",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 10m,
                    unitOfMeasure = "pcs"
                }
            }
        }), HttpStatusCode.Created);

        var (qty, value) = await StockAsync(x);
        Assert.Equal(10m, qty);
        Assert.Equal(1000m, value);

        // Verify ledger: Inventory (1300) = 1000, Clearing (2050) = -1000
        Assert.Equal(1000m, await BalanceAsync(x, "1300"));
        Assert.Equal(-1000m, await BalanceAsync(x, "2050"));

        // 3. Convert PO to Bill
        var bill = await Ok(await x.C.PostAsync($"{Pur}/orders/{poId}/convert-to-bill", null));
        var billId = (string)bill["id"]!;
        Assert.Equal(1000m, (decimal)bill["total"]!);

        // 4. Post the Bill
        var postedBill = await Ok(await x.C.PostAsync($"{Fin}/bills/{billId}/post", null));
        Assert.Equal("issued", (string)postedBill["status"]!);

        // 5. Verify clearing account is ZERO and AP equals the bill total (1000 AED) only once
        Assert.Equal(0m, await BalanceAsync(x, "2050")); // GR/IR clearing is 0
        Assert.Equal(-1000m, await BalanceAsync(x, "2000")); // AP is 1000 credit (-1000)
        Assert.Equal(1000m, await BalanceAsync(x, "1300")); // Inventory asset is 1000

        // Trial balance should be balanced
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        Assert.True((bool)tb["balanced"]!);
    }

    [Fact]
    public async Task Over_receipt_beyond_tolerance_returns_400()
    {
        var x = await SetupAsync();

        // 1. Create a Purchase Order for 10 units
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = x.Entity,
            vendorPartyId = x.VendorPartyId,
            vendorName = "Vendor Corp",
            orderDate = "2026-05-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantity = 10m,
                    unitOfMeasure = "pcs",
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 0m
                }
            }
        }), HttpStatusCode.Created);

        var poId = (string)po["id"]!;
        var poLineId = (string)po["lines"]!.AsArray()[0]!["id"]!;

        // Default tolerance is 0%. Attempting to receive 11 units should fail with 400.
        var overReceiptRes = await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-02",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 11m,
                    unitOfMeasure = "pcs"
                }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, overReceiptRes.StatusCode);

        // Update tolerance setting to 20%
        var setRes = await x.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object>
        {
            ["purchasing.receiptTolerancePercent"] = 20
        });
        Assert.Equal(HttpStatusCode.NoContent, setRes.StatusCode);

        // Now receiving 11 units (10% over, within 20% tolerance) should succeed
        var grn = await Ok(await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-02",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 11m,
                    unitOfMeasure = "pcs"
                }
            }
        }), HttpStatusCode.Created);
        Assert.NotNull(grn["id"]);

        // Attempting to receive another 3 units (11 + 3 = 14 > 12 max allowed) returns 400
        var secondOverReceipt = await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-03",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 3m,
                    unitOfMeasure = "pcs"
                }
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, secondOverReceipt.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_receipt_restores_stock_quantity_and_value_and_removes_journal_effect()
    {
        var x = await SetupAsync();

        // Check initial stock and balances: 0
        var (initQty, initVal) = await StockAsync(x);
        Assert.Equal(0m, initQty);
        Assert.Equal(0m, initVal);
        Assert.Equal(0m, await BalanceAsync(x, "1300"));
        Assert.Equal(0m, await BalanceAsync(x, "2050"));

        // 1. Create a Purchase Order for 10 units @ 100 AED
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = x.Entity,
            vendorPartyId = x.VendorPartyId,
            vendorName = "Vendor Corp",
            orderDate = "2026-05-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantity = 10m,
                    unitOfMeasure = "pcs",
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 0m
                }
            }
        }), HttpStatusCode.Created);

        var poId = (string)po["id"]!;
        var poLineId = (string)po["lines"]!.AsArray()[0]!["id"]!;

        // 2. Create Goods Receipt
        var grn = await Ok(await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-02",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 10m,
                    unitOfMeasure = "pcs"
                }
            }
        }), HttpStatusCode.Created);

        var grnId = (string)grn["id"]!;

        // Stock and ledger are updated
        var (stkQty, stkVal) = await StockAsync(x);
        Assert.Equal(10m, stkQty);
        Assert.Equal(1000m, stkVal);
        Assert.Equal(1000m, await BalanceAsync(x, "1300"));
        Assert.Equal(-1000m, await BalanceAsync(x, "2050"));

        // 3. Delete the Goods Receipt
        var delRes = await x.C.DeleteAsync($"{Pur}/receipts/{grnId}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 4. Verify stock quantity and value are restored to 0
        var (restoredQty, restoredVal) = await StockAsync(x);
        Assert.Equal(0m, restoredQty);
        Assert.Equal(0m, restoredVal);

        // 5. Verify journal effect is removed (closing balances return to 0)
        Assert.Equal(0m, await BalanceAsync(x, "1300"));
        Assert.Equal(0m, await BalanceAsync(x, "2050"));

        // PO received quantity is restored to 0
        var poAfter = await Ok(await x.C.GetAsync($"{Pur}/orders/{poId}"));
        var lineAfter = poAfter["lines"]!.AsArray()[0]!;
        Assert.Equal(0m, (decimal)lineAfter["receivedQuantity"]!);
    }

    [Fact]
    public async Task Cannot_delete_receipt_for_already_billed_purchase_order()
    {
        var x = await SetupAsync();

        // 1. Create and receive PO
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = x.Entity,
            vendorPartyId = x.VendorPartyId,
            vendorName = "Vendor Corp",
            orderDate = "2026-05-01",
            currency = "AED",
            lines = new[]
            {
                new
                {
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantity = 10m,
                    unitOfMeasure = "pcs",
                    unitPrice = 100m,
                    discountPercent = 0m,
                    taxRate = 0m
                }
            }
        }), HttpStatusCode.Created);

        var poId = (string)po["id"]!;
        var poLineId = (string)po["lines"]!.AsArray()[0]!["id"]!;

        var grn = await Ok(await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity,
            purchaseOrderId = poId,
            receivedBy = "Warehouse Manager",
            receiptDate = "2026-05-02",
            lines = new[]
            {
                new
                {
                    purchaseOrderLineId = poLineId,
                    itemId = x.ItemId,
                    itemCode = x.ItemSku,
                    description = "Raw Material",
                    quantityReceived = 10m,
                    unitOfMeasure = "pcs"
                }
            }
        }), HttpStatusCode.Created);

        var grnId = (string)grn["id"]!;

        // 2. Convert PO to bill
        await Ok(await x.C.PostAsync($"{Pur}/orders/{poId}/convert-to-bill", null));

        // 3. Attempting to delete the receipt must return 409 Conflict because PO is billed
        var delRes = await x.C.DeleteAsync($"{Pur}/receipts/{grnId}");
        Assert.Equal(HttpStatusCode.Conflict, delRes.StatusCode);
    }
}
