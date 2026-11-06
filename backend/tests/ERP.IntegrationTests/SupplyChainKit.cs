using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Shared setup for the supply chain tests: documents through the API, and the ledger and stock read back the way a user reads them.</summary>
public sealed class SupplyChainKit(HttpClient c, string entity, string vendor, string customer, string warehouse, string item, string sku)
{
    public const string Fin = "/api/v1/finance";
    public const string Inv = "/api/v1/m/inventory";
    public const string Pur = "/api/v1/m/purchasing";
    public const string Sales = "/api/v1/m/sales";

    public HttpClient C { get; } = c;
    public string Entity { get; } = entity;
    public string Vendor { get; } = vendor;
    public string Customer { get; } = customer;
    public string Warehouse { get; } = warehouse;
    public string Item { get; } = item;
    public string Sku { get; } = sku;

    public static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    public static async Task<SupplyChainKit> NewAsync(ApiFactory f, bool withWarehouse = true, params string[] modules)
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        foreach (var m in modules.Length > 0 ? modules : ["inventory", "purchasing", "sales"]) await c0.PostAsync($"/api/v1/modules/{m}/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var entity = await NewEntityAsync(c, "Chain " + tag);
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer", paymentTermsDays = 15 }), HttpStatusCode.Created))["id"]!;
        var warehouse = withWarehouse ? await NewWarehouseAsync(c, entity) : "";
        var sku = "SKU-" + tag;
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku, name = "Stocked item", type = "product", uom = "pcs", isActive = true }), HttpStatusCode.Created))["id"]!;
        return new SupplyChainKit(c, entity, vendor, customer, warehouse, item, sku);
    }

    public static async Task<string> NewEntityAsync(HttpClient c, string name)
        => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    public static async Task<string> NewWarehouseAsync(HttpClient c, string entity, string? code = null)
        => (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = code ?? "W" + Guid.NewGuid().ToString("N")[..6], name = "Warehouse", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;

    public async Task<string> NewTaxCodeAsync(decimal rate)
        => (string)(await Ok(await C.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "T" + Guid.NewGuid().ToString("N")[..6], name = $"VAT {rate}", kind = "vat", rate }), HttpStatusCode.Created))["id"]!;

    // ---- ledger and stock

    public async Task<decimal> BalanceAsync(string systemRole, string? entity = null)
    {
        var account = (await Ok(await C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["systemRole"] == systemRole)!;
        var tb = await Ok(await C.GetAsync($"{Fin}/reports/trial-balance?to=2099-12-31&legalEntityId={entity ?? Entity}"));
        Assert.True((bool)tb["balanced"]!, "The trial balance must stay balanced.");
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == (string)account["code"]!)?["closing"] ?? 0m;
    }

    public async Task<(decimal Quantity, decimal Value)> StockAsync(string? warehouse = null, string? item = null)
    {
        var report = await Ok(await C.GetAsync($"{Inv}/stock?itemId={item ?? Item}" + (warehouse is null ? "" : $"&warehouseId={warehouse}")));
        var rows = report["rows"]!.AsArray();
        return (rows.Sum(r => (decimal)r!["quantity"]!), (decimal)report["totalValue"]!);
    }

    public async Task OpeningStockAsync(decimal quantity, decimal unitCost, string? warehouse = null, string date = "2026-01-02")
    {
        var offset = (string)(await Ok(await C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["systemRole"] == "inventory.clearing")!["id"]!;
        await Ok(await C.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = warehouse ?? Warehouse, itemId = Item, quantity, unitCost, date, offsetAccountId = offset, reference = "OPENING" }), HttpStatusCode.Created);
    }

    // ---- purchasing

    public async Task<(string Id, string LineId)> ApprovedOrderAsync(decimal quantity = 10m, decimal price = 100m, decimal taxRate = 0m, string? entity = null, string? item = null, string? vendorReference = null)
    {
        var po = await Ok(await C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = entity ?? Entity, vendorPartyId = Vendor, vendorName = "Vendor", orderDate = "2026-05-01", currency = "AED", vendorReference,
            lines = new[] { new { itemId = item ?? Item, itemCode = Sku, description = "Stocked item", quantity, unitOfMeasure = "pcs", unitPrice = price, discountPercent = 0m, taxRate } },
        }), HttpStatusCode.Created);
        var id = (string)po["id"]!;
        await Ok(await C.PutAsJsonAsync($"{Pur}/orders/{id}/status", new { status = "Approved" }));
        return (id, (string)po["lines"]!.AsArray()[0]!["id"]!);
    }

    public Task<HttpResponseMessage> ReceiveAsync(string poId, string lineId, decimal quantity, string? entity = null, string? warehouseId = null, string date = "2026-05-02")
        => C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = entity ?? Entity, purchaseOrderId = poId, receivedBy = "Storekeeper", receiptDate = date, warehouseId,
            lines = new[] { new { purchaseOrderLineId = lineId, description = "Stocked item", quantityReceived = quantity } },
        });

    public async Task<JsonNode> ReceivedAsync(string poId, string lineId, decimal quantity, string? warehouseId = null)
        => await Ok(await ReceiveAsync(poId, lineId, quantity, warehouseId: warehouseId), HttpStatusCode.Created);

    public async Task<JsonNode> BillAsync(string poId, object? body = null)
        => await Ok(body is null ? await C.PostAsync($"{Pur}/orders/{poId}/convert-to-bill", null) : await C.PostAsJsonAsync($"{Pur}/orders/{poId}/convert-to-bill", body));

    public async Task<JsonNode> PostBillAsync(JsonNode bill) => await Ok(await C.PostAsync($"{Fin}/bills/{(string)bill["id"]!}/post", null));

    public async Task<JsonNode> OrderAsync(string poId) => await Ok(await C.GetAsync($"{Pur}/orders/{poId}"));

    // ---- sales

    public async Task<(string Id, string LineId)> ConfirmedSaleAsync(decimal quantity = 5m, decimal price = 200m, decimal taxRate = 0m, bool confirm = true)
    {
        var so = await Ok(await C.PostAsJsonAsync($"{Sales}/orders", new
        {
            legalEntityId = Entity, customerPartyId = Customer, customerName = "Customer", orderDate = "2026-06-01", currency = "AED",
            lines = new[] { new { itemId = Item, itemCode = Sku, description = "Stocked item", quantity, unitOfMeasure = "pcs", unitPrice = price, discountPercent = 0m, taxRate } },
        }), HttpStatusCode.Created);
        var id = (string)so["id"]!;
        if (confirm) await Ok(await C.PutAsJsonAsync($"{Sales}/orders/{id}/status", new { status = "Confirmed" }));
        return (id, (string)so["lines"]!.AsArray()[0]!["id"]!);
    }

    public Task<HttpResponseMessage> DeliverAsync(string orderId, string lineId, decimal delivered, string? date = null, string? warehouseId = null)
        => C.PutAsJsonAsync($"{Sales}/orders/{orderId}/fulfillment", new { lines = new[] { new { lineId, deliveredQuantity = delivered } }, deliveryDate = date, warehouseId });

    public async Task<JsonNode> SaleAsync(string id) => await Ok(await C.GetAsync($"{Sales}/orders/{id}"));

    /// <summary>A user with the built-in Approver role, for maker-checker tests.</summary>
    public async Task<HttpClient> NewApproverAsync(ApiFactory f)
    {
        var roles = await Ok(await C.GetAsync("/api/v1/roles"));
        var role = (string)roles.AsArray().First(r => (string?)r!["name"] == "Approver")!["id"]!;
        var email = "ap" + Guid.NewGuid().ToString("N")[..12] + "@demo.local";
        await Ok(await C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Approver", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { role } }), HttpStatusCode.Created);
        return (await f.SignInAsync(email, "Temp#Passw0rd1")).Client;
    }
}
