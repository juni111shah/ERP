using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>Goods receipts only for approved orders of the same legal entity, item data from the order line, per-line totals, dates and precision (TASK-131, TASK-132).</summary>
public class GoodsReceiptRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Inv = "/api/v1/m/inventory";
    private const string Pur = "/api/v1/m/purchasing";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Vendor, string ItemId, string ItemSku, string OtherItemId, string OtherSku);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/purchasing/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "GrRules " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + tag, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = "WH-" + tag[..4], name = "Warehouse", legalEntityId = entity }), HttpStatusCode.Created);

        async Task<(string Id, string Sku)> NewItem(string prefix)
        {
            var sku = prefix + "-" + Guid.NewGuid().ToString("N")[..6];
            var id = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku, name = prefix + " item", type = "product", uom = "pcs", purchasePrice = 100m, isActive = true }), HttpStatusCode.Created))["id"]!;
            return (id, sku);
        }
        var (item, sku) = await NewItem("RAW");
        var (other, otherSku) = await NewItem("OTH");
        return new Ctx(c, entity, vendor, item, sku, other, otherSku);
    }

    /// <summary>Creates a purchase order of <paramref name="quantity"/> units of the main item at 100 each; returns the order id and its line id.</summary>
    private static async Task<(string Id, string LineId)> OrderAsync(Ctx x, decimal quantity = 10m, string? approveTo = "Approved")
    {
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", new
        {
            legalEntityId = x.Entity, vendorPartyId = x.Vendor, vendorName = "Vendor", orderDate = "2026-05-01", currency = "AED",
            lines = new[] { new { itemId = x.ItemId, itemCode = x.ItemSku, description = "Raw Material", quantity, unitOfMeasure = "pcs", unitPrice = 100m, discountPercent = 0m, taxRate = 0m } },
        }), HttpStatusCode.Created);
        var id = (string)po["id"]!;
        if (approveTo is not null) await Ok(await x.C.PutAsJsonAsync($"{Pur}/orders/{id}/status", new { status = approveTo }));
        return (id, (string)po["lines"]!.AsArray()[0]!["id"]!);
    }

    private static object Line(Ctx x, string lineId, decimal quantity, string? itemId) => new
    {
        purchaseOrderLineId = lineId, itemId, itemCode = x.ItemSku, description = "Raw Material", quantityReceived = quantity, unitOfMeasure = "pcs",
    };

    private static Task<HttpResponseMessage> ReceiveAsync(Ctx x, string poId, object[] lines, string date = "2026-05-02", string? entity = null)
        => x.C.PostAsJsonAsync($"{Pur}/receipts", new { legalEntityId = entity ?? x.Entity, purchaseOrderId = poId, receivedBy = "Warehouse Manager", receiptDate = date, lines });

    private static async Task<decimal> StockAsync(Ctx x, string itemId)
    {
        var report = await Ok(await x.C.GetAsync($"{Inv}/stock?itemId={itemId}"));
        var rows = report["rows"]!.AsArray();
        return rows.Count == 0 ? 0m : rows.Sum(r => (decimal)r!["quantity"]!);
    }

    // ---- TASK-131

    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    [InlineData("Cancelled")]
    public async Task A_receipt_against_a_draft_submitted_or_cancelled_order_is_rejected_and_moves_no_stock(string status)
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x, approveTo: status == "Draft" ? null : status);
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 5m, x.ItemId)]);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("approved", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await StockAsync(x, x.ItemId));
    }

    [Fact]
    public async Task A_receipt_against_a_billed_order_is_rejected()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        await Ok(await ReceiveAsync(x, poId, [Line(x, lineId, 10m, x.ItemId)]), HttpStatusCode.Created);
        await Ok(await x.C.PostAsync($"{Pur}/orders/{poId}/convert-to-bill", null));
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 1m, x.ItemId)]);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal(10m, await StockAsync(x, x.ItemId));
    }

    [Fact]
    public async Task A_receipt_for_another_legal_entity_is_rejected_on_legalEntityId()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        var other = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Other " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 5m, x.ItemId)], entity: other);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("legalEntityId", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await StockAsync(x, x.ItemId));
    }

    [Fact]
    public async Task A_line_with_another_item_than_the_order_line_is_rejected_and_moves_no_stock()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 5m, x.OtherItemId)]);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("lines", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await StockAsync(x, x.ItemId));
        Assert.Equal(0m, await StockAsync(x, x.OtherItemId));
    }

    [Fact]
    public async Task A_line_without_an_item_takes_the_item_data_from_the_order_line()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        var grn = await Ok(await ReceiveAsync(x, poId, [new { purchaseOrderLineId = lineId, itemId = (string?)null, itemCode = "WRONG", description = "Wrong text", quantityReceived = 4m, unitOfMeasure = "kg" }]), HttpStatusCode.Created);
        var line = grn["lines"]!.AsArray()[0]!;
        Assert.Equal(x.ItemId, (string)line["itemId"]!);
        Assert.Equal(x.ItemSku, (string)line["itemCode"]!);
        Assert.Equal("Raw Material", (string)line["description"]!);
        Assert.Equal("pcs", (string)line["unitOfMeasure"]!);
        Assert.Equal(4m, await StockAsync(x, x.ItemId));
        Assert.Equal(0m, await StockAsync(x, x.OtherItemId));
    }

    [Fact]
    public async Task A_normal_receipt_against_an_approved_order_updates_the_order_status()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        await Ok(await ReceiveAsync(x, poId, [Line(x, lineId, 4m, x.ItemId)]), HttpStatusCode.Created);
        Assert.Equal("partiallyReceived", (string)(await Ok(await x.C.GetAsync($"{Pur}/orders/{poId}")))["status"]!, ignoreCase: true);
        await Ok(await ReceiveAsync(x, poId, [Line(x, lineId, 6m, x.ItemId)]), HttpStatusCode.Created);
        Assert.Equal("received", (string)(await Ok(await x.C.GetAsync($"{Pur}/orders/{poId}")))["status"]!, ignoreCase: true);
        Assert.Equal(10m, await StockAsync(x, x.ItemId));
    }

    // ---- TASK-132

    [Fact]
    public async Task Two_lines_for_the_same_order_line_are_checked_together()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x, quantity: 10m);
        // each line alone fits (6 <= 10) but together they are 12 > 10 with 0% tolerance
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 6m, x.ItemId), Line(x, lineId, 6m, x.ItemId)]);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("lines", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await StockAsync(x, x.ItemId));
    }

    [Fact]
    public async Task Two_lines_for_the_same_order_line_that_fit_together_are_accepted()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x, quantity: 10m);
        await Ok(await ReceiveAsync(x, poId, [Line(x, lineId, 6m, x.ItemId), Line(x, lineId, 4m, x.ItemId)]), HttpStatusCode.Created);
        Assert.Equal(10m, await StockAsync(x, x.ItemId));
    }

    [Theory]
    [InlineData("0001-01-01")]
    [InlineData("1999-12-31")]
    [InlineData("2099-01-01")]
    public async Task Implausible_receipt_dates_are_rejected_on_receiptDate(string date)
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        var res = await ReceiveAsync(x, poId, [Line(x, lineId, 5m, x.ItemId)], date);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("receiptdate", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(0m, await StockAsync(x, x.ItemId));
    }

    [Fact]
    public async Task A_quantity_with_five_decimals_is_rejected_and_four_are_accepted()
    {
        var x = await SetupAsync();
        var (poId, lineId) = await OrderAsync(x);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReceiveAsync(x, poId, [Line(x, lineId, 1.00001m, x.ItemId)])).StatusCode);
        await Ok(await ReceiveAsync(x, poId, [Line(x, lineId, 1.2345m, x.ItemId)]), HttpStatusCode.Created);
    }
}
