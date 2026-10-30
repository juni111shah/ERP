using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Domain.Finance;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>Purchase order edit/delete guards, PO-to-bill conversion (tax, due date, role accounts, idempotence) and header validation (TASK-133 to 136).</summary>
public class PurchaseOrderRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
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

    private sealed record Ctx(HttpClient C, string Entity, string Vendor, string VendorName, string Customer, string ItemId, string ItemSku);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        await c0.PostAsync("/api/v1/modules/purchasing/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];

        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "PoRules " + tag, countryCode = "AE", baseCurrency = "AED", isTaxRegistered = true }), HttpStatusCode.Created))["id"]!;
        var vendorName = "Vendor " + tag;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = vendorName, kind = "Company", roles = "Vendor", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Customer " + tag, kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = "WH-" + tag[..4], name = "Warehouse", legalEntityId = entity }), HttpStatusCode.Created);
        var sku = "RAW-" + tag;
        var item = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new { sku, name = "Raw Material", type = "product", uom = "pcs", purchasePrice = 100m, isActive = true }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity, vendor, vendorName, customer, item, sku);
    }

    private static Dictionary<string, object?> PoBody(Ctx x, string? entity = null, string? vendor = "vendor", string? vendorName = null, string orderDate = "2026-05-01",
        string? delivery = null, string currency = "AED", decimal quantity = 10m, decimal price = 100m, decimal taxRate = 0m, string notes = "")
        => new()
        {
            ["legalEntityId"] = entity ?? x.Entity,
            ["vendorPartyId"] = vendor == "vendor" ? x.Vendor : vendor,
            ["vendorName"] = vendorName ?? x.VendorName,
            ["orderDate"] = orderDate,
            ["expectedDeliveryDate"] = delivery,
            ["currency"] = currency,
            ["notes"] = notes,
            ["lines"] = new[] { new { itemId = x.ItemId, itemCode = x.ItemSku, description = "Raw Material", quantity, unitOfMeasure = "pcs", unitPrice = price, discountPercent = 0m, taxRate } },
        };

    private static async Task<(string Id, string LineId, JsonNode Order)> CreatePoAsync(Ctx x, Dictionary<string, object?>? body = null)
    {
        var po = await Ok(await x.C.PostAsJsonAsync($"{Pur}/orders", body ?? PoBody(x)), HttpStatusCode.Created);
        return ((string)po["id"]!, (string)po["lines"]!.AsArray()[0]!["id"]!, po);
    }

    private static Task<JsonNode> SetStatusAsync(Ctx x, string id, string status) => x.C.PutAsJsonAsync($"{Pur}/orders/{id}/status", new { status }).ContinueWith(t => Ok(t.Result)).Unwrap();

    private static async Task ReceiveAsync(Ctx x, string poId, string lineId, decimal quantity)
        => await Ok(await x.C.PostAsJsonAsync($"{Pur}/receipts", new
        {
            legalEntityId = x.Entity, purchaseOrderId = poId, receivedBy = "Storekeeper", receiptDate = "2026-05-02",
            lines = new[] { new { purchaseOrderLineId = lineId, itemId = x.ItemId, itemCode = x.ItemSku, description = "Raw Material", quantityReceived = quantity, unitOfMeasure = "pcs" } },
        }), HttpStatusCode.Created);

    /// <summary>Puts a fresh order into the named status.</summary>
    private static async Task<(string Id, string LineId)> OrderInAsync(Ctx x, string status)
    {
        var (id, line, _) = await CreatePoAsync(x);
        switch (status)
        {
            case "Draft": break;
            case "Submitted": await SetStatusAsync(x, id, "Submitted"); break;
            case "Cancelled": await SetStatusAsync(x, id, "Cancelled"); break;
            case "Approved": await SetStatusAsync(x, id, "Approved"); break;
            case "PartiallyReceived": await SetStatusAsync(x, id, "Approved"); await ReceiveAsync(x, id, line, 4m); break;
            case "Received": await SetStatusAsync(x, id, "Approved"); await ReceiveAsync(x, id, line, 10m); break;
            case "Billed":
                await SetStatusAsync(x, id, "Approved"); await ReceiveAsync(x, id, line, 10m);
                await Ok(await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null));
                break;
            default: throw new ArgumentException(status);
        }
        return (id, line);
    }

    private static async Task<int> BillCountAsync(Ctx x)
    {
        var page = await Ok(await x.C.GetAsync($"{Fin}/bills?pageSize=200"));
        var items = page is JsonArray a ? a : page["items"]!.AsArray();
        return items.Count(b => (string?)b!["partyId"] == x.Vendor);
    }

    // ---- TASK-133 edit and delete guards

    [Theory]
    [InlineData("Approved")]
    [InlineData("PartiallyReceived")]
    [InlineData("Received")]
    [InlineData("Cancelled")]
    [InlineData("Billed")]
    public async Task Editing_an_order_that_is_past_submitted_returns_409_and_changes_nothing(string status)
    {
        var x = await SetupAsync();
        var (id, _) = await OrderInAsync(x, status);
        var before = await Ok(await x.C.GetAsync($"{Pur}/orders/{id}"));

        var res = await x.C.PutAsJsonAsync($"{Pur}/orders/{id}", PoBody(x, quantity: 99m, notes: "changed after approval"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);

        var after = await Ok(await x.C.GetAsync($"{Pur}/orders/{id}"));
        Assert.Equal((string)before["status"]!, (string)after["status"]!);
        Assert.Equal((decimal)before["totalAmount"]!, (decimal)after["totalAmount"]!);
        Assert.Equal((string?)before["notes"], (string?)after["notes"]);
        Assert.Equal((string)before["lines"]![0]!["id"]!, (string)after["lines"]![0]!["id"]!); // the lines were not recreated
        Assert.Equal((decimal)before["lines"]![0]!["receivedQuantity"]!, (decimal)after["lines"]![0]!["receivedQuantity"]!);
    }

    // PRODUCTION BUG (not caused by TASK-133): PUT on a Draft/Submitted order returns 500 "Cross-workspace write blocked".
    // OrderFeatures.cs PopulateLinesAndTotals does order.Lines.Clear() and adds new PurchaseOrderLine objects that already carry a key,
    // so EF tracks them as Modified (not Added) with an empty OrgId and AuditSaveChangesInterceptor rejects the save. The invoice update
    // avoids this by removing/adding lines through the line repository. Remove the Skip once that is fixed.
    [Theory]
    [InlineData("Draft")]
    [InlineData("Submitted")]
    public async Task A_draft_or_submitted_order_can_still_be_edited(string status)
    {
        var x = await SetupAsync();
        var (id, _) = await OrderInAsync(x, status);
        var updated = await Ok(await x.C.PutAsJsonAsync($"{Pur}/orders/{id}", PoBody(x, quantity: 20m, notes: "revised")));
        Assert.Equal(2000m, (decimal)updated["totalAmount"]!);
        Assert.Equal("revised", (string)updated["notes"]!);
    }

    [Fact]
    public async Task Changing_the_legal_entity_on_update_returns_400()
    {
        var x = await SetupAsync();
        var (id, _, _) = await CreatePoAsync(x);
        var other = (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Other " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var res = await x.C.PutAsJsonAsync($"{Pur}/orders/{id}", PoBody(x, entity: other));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("legalentityid", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("PartiallyReceived")] // has a goods receipt
    [InlineData("Received")]
    [InlineData("Billed")]
    public async Task Deleting_an_approved_or_received_order_returns_409(string status)
    {
        var x = await SetupAsync();
        var (id, _) = await OrderInAsync(x, status);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.DeleteAsync($"{Pur}/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await x.C.GetAsync($"{Pur}/orders/{id}")).StatusCode);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Cancelled")]
    public async Task Deleting_a_draft_or_cancelled_order_works(string status)
    {
        var x = await SetupAsync();
        var (id, _) = await OrderInAsync(x, status);
        Assert.Equal(HttpStatusCode.NoContent, (await x.C.DeleteAsync($"{Pur}/orders/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await x.C.GetAsync($"{Pur}/orders/{id}")).StatusCode);
    }

    // ---- TASK-134 tax and due date on conversion

    private async Task<string> TaxCodeAsync(Ctx x, decimal rate)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "P" + Guid.NewGuid().ToString("N")[..6], name = $"VAT {rate}", kind = "vat", rate }), HttpStatusCode.Created))["id"]!;

    [Fact]
    public async Task A_five_percent_order_converts_to_a_bill_with_the_same_tax_and_total()
    {
        var x = await SetupAsync();
        await TaxCodeAsync(x, 5m);
        var (id, _, po) = await CreatePoAsync(x, PoBody(x, taxRate: 5m));
        Assert.Equal((50m, 1050m), ((decimal)po["taxTotal"]!, (decimal)po["totalAmount"]!));
        await SetStatusAsync(x, id, "Approved");

        var bill = await Ok(await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null));
        Assert.Equal((decimal)po["taxTotal"]!, (decimal)bill["tax"]!);
        Assert.Equal((decimal)po["totalAmount"]!, (decimal)bill["total"]!);
    }

    [Fact]
    public async Task An_order_with_a_past_delivery_date_still_converts_with_a_valid_due_date()
    {
        var x = await SetupAsync();
        var (id, _, _) = await CreatePoAsync(x, PoBody(x, orderDate: "2019-12-01", delivery: "2020-01-15"));
        await SetStatusAsync(x, id, "Approved");

        var bill = await Ok(await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null));
        var billDate = DateOnly.Parse((string)bill["billDate"]!);
        var dueDate = DateOnly.Parse((string)bill["dueDate"]!);
        Assert.True(dueDate >= billDate, $"due {dueDate} is before bill date {billDate}");
    }

    [Fact]
    public async Task A_tax_rate_with_no_tax_code_returns_409_and_creates_no_bill()
    {
        var x = await SetupAsync();
        var (id, _, _) = await CreatePoAsync(x, PoBody(x, taxRate: 7.35m));
        await SetStatusAsync(x, id, "Approved");

        var res = await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("7.35", await res.Content.ReadAsStringAsync());
        Assert.Equal(0, await BillCountAsync(x));
        var order = await Ok(await x.C.GetAsync($"{Pur}/orders/{id}"));
        Assert.Equal("approved", ((string)order["status"]!).ToLowerInvariant());
        Assert.Null((string?)order["invoicedBillId"]);
    }

    // ---- TASK-135 conversion is atomic, idempotent, role-based

    [Fact]
    public async Task Converting_twice_returns_the_same_bill()
    {
        var x = await SetupAsync();
        var (id, _, _) = await CreatePoAsync(x);
        await SetStatusAsync(x, id, "Approved");
        var first = await Ok(await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null));
        var second = await Ok(await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null));
        Assert.Equal((string)first["id"]!, (string)second["id"]!);
        Assert.Equal(1, await BillCountAsync(x));
    }

    [Fact]
    public async Task A_missing_purchases_expense_account_returns_409_naming_the_role_and_creates_no_bill()
    {
        var x = await SetupAsync();
        var (id, _, _) = await CreatePoAsync(x);
        await SetStatusAsync(x, id, "Approved");

        // The API refuses to deactivate a system account, so the role is cleared in the database and restored afterwards.
        var accountId = (string)(await Ok(await x.C.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "purchases.expense")!["id"]!;
        var id6900 = Guid.Parse(accountId);
        async Task SetRoleAsync(string? role)
        {
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<Account>().IgnoreQueryFilters().Where(a => a.Id == id6900).ExecuteUpdateAsync(s => s.SetProperty(a => a.SystemRole, role));
        }

        await SetRoleAsync(null);
        try
        {
            var res = await x.C.PostAsync($"{Pur}/orders/{id}/convert-to-bill", null);
            Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
            Assert.Contains("purchases.expense", await res.Content.ReadAsStringAsync());
            Assert.Equal(0, await BillCountAsync(x));
            Assert.Null((string?)(await Ok(await x.C.GetAsync($"{Pur}/orders/{id}")))["invoicedBillId"]);
        }
        finally
        {
            await SetRoleAsync("purchases.expense"); // the workspace is shared by the other tests of this class
        }
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.12345")]
    public async Task Negative_or_five_decimal_override_prices_return_400(string price)
    {
        var x = await SetupAsync();
        var (id, line, _) = await CreatePoAsync(x);
        await SetStatusAsync(x, id, "Approved");
        var overrides = new JsonObject { ["unitPriceOverrides"] = new JsonObject { [line] = JsonNode.Parse(price) } };
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders/{id}/convert-to-bill", overrides);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("unitpriceoverrides", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
        Assert.Equal(0, await BillCountAsync(x));
    }

    // ---- TASK-136 header validation

    [Fact]
    public async Task A_customer_only_party_cannot_be_the_vendor()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, vendor: x.Customer));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("partyid", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task An_unknown_vendor_party_returns_400()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, vendor: Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("vendorpartyid", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_currency_other_than_the_entity_currency_returns_400()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, currency: "USD"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("currency", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_year_one_order_date_returns_400()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, orderDate: "0001-01-01"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("orderdate", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_delivery_date_before_the_order_date_returns_400()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, orderDate: "2026-05-10", delivery: "2026-05-01"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("expecteddeliverydate", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_five_decimal_unit_price_returns_400()
    {
        var x = await SetupAsync();
        var res = await x.C.PostAsJsonAsync($"{Pur}/orders", PoBody(x, price: 9.12345m));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("unitprice", (await res.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task A_normal_vendor_order_works_and_takes_the_vendor_name_from_the_party()
    {
        var x = await SetupAsync();
        var (_, _, po) = await CreatePoAsync(x, PoBody(x, vendorName: "Some typed name", delivery: "2026-06-01", price: 9.1234m));
        Assert.Equal(x.VendorName, (string)po["vendorName"]!);
        Assert.Equal("AED", (string)po["currency"]!);
    }
}
