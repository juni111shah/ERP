using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Application.Security;
using ERP.Domain.Catalog;
using ERP.Modules.Pos;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>The till as a shop uses it: returns, held sales, on-account sales, cash movements in the ledger, shift and period reports.</summary>
public class PosTillTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pos = "/api/v1/m/pos";

    private static async Task<JsonNode> Json(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Till(HttpClient C, Guid Entity, Guid Register, Guid Session);

    private async Task<Till> OpenTillAsync(decimal opening = 100m, object? registerExtras = null)
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;
        var reg = await Json(await c.PostAsJsonAsync($"{Pos}/registers", new { legalEntityId = entity, code = "T-" + Guid.NewGuid().ToString("N")[..6], name = "Till" }));
        var session = await Json(await c.PostAsJsonAsync($"{Pos}/sessions/open", new { legalEntityId = entity, registerId = (string)reg["id"]!, openingCash = opening }));
        return new Till(c, Guid.Parse(entity), Guid.Parse((string)reg["id"]!), Guid.Parse((string)session["id"]!));
    }

    private static async Task<Guid> ItemAsync(Till t, decimal price = 100m, Guid? tax = null, string? barcode = null, ItemType type = ItemType.Service)
    {
        var res = await t.C.PostAsJsonAsync("/api/v1/items", new ItemInput("TI-" + Guid.NewGuid().ToString("N")[..8], "Till item", type, "ea", price, price / 2, tax, Barcode: barcode));
        return Guid.Parse((string)(await Json(res))["id"]!);
    }

    private static async Task<Guid> TaxAsync(Till t, decimal rate = 5m)
    {
        var res = await t.C.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "TX" + Guid.NewGuid().ToString("N")[..6], name = "Tax", kind = "vat", rate, isDefault = false, legalEntityId = t.Entity });
        return Guid.Parse((string)(await Json(res))["id"]!);
    }

    private static async Task<string> CustomerAsync(Till t)
    {
        var res = await t.C.PostAsJsonAsync("/api/v1/parties", new { name = "Till customer " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer" });
        return (string)(await Json(res))["id"]!;
    }

    private static Task<HttpResponseMessage> SellAsync(Till t, Guid item, decimal qty, decimal total, PosPaymentMethod method = PosPaymentMethod.Cash, Guid? customer = null, decimal discount = 0, Guid? parked = null) =>
        t.C.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, customer, null,
            [new PosOrderLineInput(item, Quantity: qty, DiscountPercent: discount)],
            [new PosPaymentInput(method, total, total, 0m, null)], ParkedOrderId: parked));

    private static Task<HttpResponseMessage> ReturnAsync(Till t, JsonNode order, decimal qty, decimal refund, PosPaymentMethod method = PosPaymentMethod.Cash, bool restock = true, string reason = "Changed mind") =>
        t.C.PostAsJsonAsync($"{Pos}/returns", new CreatePosReturnCommand(Guid.Parse((string)order["id"]!), t.Session, reason,
            [new PosReturnLineInput(Guid.Parse((string)order["lines"]![0]!["id"]!), qty, restock)],
            [new PosRefundInput(method, refund)]));

    private static async Task<Dictionary<string, decimal>> BalancesAsync(Till t)
    {
        var tb = await Json(await t.C.GetAsync($"/api/v1/finance/reports/trial-balance?to=2099-12-31&legalEntityId={t.Entity}"));
        Assert.True((bool)tb["balanced"]!, "trial balance must balance");
        return tb["rows"]!.AsArray().ToDictionary(r => (string)r!["code"]!, r => (decimal)r!["debit"]! - (decimal)r["credit"]!);
    }

    private static decimal Delta(Dictionary<string, decimal> before, Dictionary<string, decimal> after, string code) => after.GetValueOrDefault(code) - before.GetValueOrDefault(code);

    private static async Task<decimal> ExpectedCashAsync(Till t) => (decimal)(await Json(await t.C.GetAsync($"{Pos}/sessions/current?registerId={t.Register}")))["expectedCash"]!;

    private async Task<HttpClient> SignInWithRoleAsync(Till t, params string[] permissions)
    {
        var role = await Json(await t.C.PostAsJsonAsync("/api/v1/roles", new RoleInput("Role_" + Guid.NewGuid().ToString("N")[..8], "Till test role", permissions)));
        var email = $"till_{Guid.NewGuid():N}@demo.local";
        (await t.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Till User", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { Guid.Parse((string)role["id"]!) } })).EnsureSuccessStatusCode();
        var (client, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        return client;
    }

    // ---- the till itself ----

    [Fact]
    public async Task A_user_who_can_only_sell_still_gets_registers_items_customers_and_held_sales()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 40m, tax);
        await CustomerAsync(t);
        var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate);

        var terminal = await Json(await seller.GetAsync($"{Pos}/terminal"));
        var row = terminal["registers"]!.AsArray().First(r => (Guid)r!["id"]! == t.Register)!;
        Assert.Equal(t.Session, (Guid)row["openSessionId"]!);
        Assert.Equal(100m, (decimal)terminal["settings"]!["maxDiscountPercent"]!);

        var catalog = await Json(await seller.GetAsync($"{Pos}/terminal/catalog?registerId={t.Register}&pageSize=200"));
        var hit = catalog["items"]!.AsArray().First(i => (Guid)i!["id"]! == item)!;
        Assert.Equal(40m, (decimal)hit["price"]!);
        Assert.Equal(5m, (decimal)hit["taxRate"]!);

        Assert.NotEmpty((await Json(await seller.GetAsync($"{Pos}/terminal/customers?search=Till"))).AsArray());
        Assert.Equal(t.Session, (Guid)(await Json(await seller.GetAsync($"{Pos}/terminal/session?registerId={t.Register}")))["id"]!);
        Assert.Equal(HttpStatusCode.OK, (await seller.GetAsync($"{Pos}/terminal/held?registerId={t.Register}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync($"{Pos}/registers")).StatusCode);
    }

    [Fact]
    public async Task A_scan_finds_the_item_by_barcode_or_sku_and_a_barcode_belongs_to_one_item()
    {
        var t = await OpenTillAsync();
        var code = "62" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        var item = await ItemAsync(t, 12m, barcode: code);

        var scan = await Json(await t.C.GetAsync($"{Pos}/terminal/catalog?code={code}"));
        Assert.Equal(item, (Guid)scan["items"]![0]!["id"]!);
        Assert.Equal(1, (int)scan["total"]!);

        var clash = await t.C.PostAsJsonAsync("/api/v1/items", new ItemInput("TI-" + Guid.NewGuid().ToString("N")[..8], "Other", ItemType.Service, "ea", 1m, 1m, null, Barcode: code));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        // A caller that does not know about barcodes must not wipe one.
        var dto = await Json(await t.C.GetAsync($"/api/v1/items/{item}"));
        (await t.C.PutAsJsonAsync($"/api/v1/items/{item}", new ItemInput((string)dto["sku"]!, "Renamed", ItemType.Service, "ea", 12m, 6m, null))).EnsureSuccessStatusCode();
        Assert.Equal(code, (string)(await Json(await t.C.GetAsync($"/api/v1/items/{item}")))["barcode"]!);
    }

    [Fact]
    public async Task Inactive_items_and_items_without_a_price_cannot_be_sold()
    {
        var t = await OpenTillAsync();
        var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate);
        var inactive = await Json(await t.C.PostAsJsonAsync("/api/v1/items", new ItemInput("TI-" + Guid.NewGuid().ToString("N")[..8], "Gone", ItemType.Service, "ea", 10m, 5m, null, IsActive: false)));
        var noPrice = await Json(await t.C.PostAsJsonAsync("/api/v1/items", new ItemInput("TI-" + Guid.NewGuid().ToString("N")[..8], "Unpriced", ItemType.Service, "ea", null, null, null)));

        var a = await seller.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, null, null,
            [new PosOrderLineInput(Guid.Parse((string)inactive["id"]!))], [new PosPaymentInput(PosPaymentMethod.Cash, 10m, 10m, 0, null)]));
        Assert.Equal(HttpStatusCode.Conflict, a.StatusCode);

        var b = await seller.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, null, null,
            [new PosOrderLineInput(Guid.Parse((string)noPrice["id"]!))], [new PosPaymentInput(PosPaymentMethod.Cash, 1m, 1m, 0, null)]));
        Assert.Equal(HttpStatusCode.BadRequest, b.StatusCode);
    }

    [Fact]
    public async Task The_cashier_discount_limit_stops_a_seller_but_not_someone_with_the_override()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        Assert.Equal(HttpStatusCode.NoContent, (await t.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["pos.cashierDiscountPercent"] = 10 })).StatusCode);
        try
        {
            var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate);
            var body = new CreatePosOrderCommand(t.Entity, t.Register, t.Session, null, null, [new PosOrderLineInput(item, DiscountPercent: 15)], [new PosPaymentInput(PosPaymentMethod.Cash, 85m, 85m, 0, null)]);
            Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync($"{Pos}/orders", body)).StatusCode);

            var order = await Json(await t.C.PostAsJsonAsync($"{Pos}/orders", body)); // admin holds the override
            Assert.Equal(15m, (decimal)order["discountTotal"]!);
            Assert.Equal(85m, (decimal)order["totalAmount"]!);
        }
        finally
        {
            await t.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object?> { ["pos.cashierDiscountPercent"] = null });
        }
    }

    // ---- held sales ----

    [Fact]
    public async Task A_held_sale_touches_no_money_and_completes_once()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 30m);
        var before = await BalancesAsync(t);

        var held = await Json(await t.C.PostAsJsonAsync($"{Pos}/orders/park", new ParkPosOrderCommand(t.Session, null, "Back in five", [new PosOrderLineInput(item, Quantity: 2)], "Went for a wallet")));
        Assert.Equal("parked", (string)held["status"]!);
        Assert.Equal(60m, (decimal)held["totalAmount"]!);
        Assert.StartsWith("HOLD-", (string)held["number"]!);
        Assert.Equal(before["1000"], (await BalancesAsync(t))["1000"]);
        Assert.Equal(100m, await ExpectedCashAsync(t));

        var list = await Json(await t.C.GetAsync($"{Pos}/terminal/held?registerId={t.Register}"));
        Assert.Contains(list.AsArray(), o => (string)o!["id"]! == (string)held["id"]!);

        var heldId = Guid.Parse((string)held["id"]!);
        var sold = await Json(await SellAsync(t, item, 2, 60m, parked: heldId));
        Assert.StartsWith("POS", (string)sold["number"]!);
        Assert.Equal(60m, Delta(before, await BalancesAsync(t), "1000"));

        Assert.DoesNotContain((await Json(await t.C.GetAsync($"{Pos}/terminal/held?registerId={t.Register}"))).AsArray(), o => (string)o!["id"]! == heldId.ToString());
        Assert.Equal(HttpStatusCode.Conflict, (await SellAsync(t, item, 2, 60m, parked: heldId)).StatusCode); // one held ticket cannot be charged twice
    }

    [Fact]
    public async Task A_held_sale_can_be_discarded_and_only_held_sales_can()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 30m);
        var held = await Json(await t.C.PostAsJsonAsync($"{Pos}/orders/park", new ParkPosOrderCommand(t.Session, null, null, [new PosOrderLineInput(item)])));
        Assert.Equal(HttpStatusCode.NoContent, (await t.C.DeleteAsync($"{Pos}/orders/{(string)held["id"]!}/park")).StatusCode);
        Assert.Empty((await Json(await t.C.GetAsync($"{Pos}/terminal/held?registerId={t.Register}"))).AsArray());

        var sold = await Json(await SellAsync(t, item, 1, 30m));
        Assert.Equal(HttpStatusCode.Conflict, (await t.C.DeleteAsync($"{Pos}/orders/{(string)sold["id"]!}/park")).StatusCode);
    }

    // ---- returns ----

    [Fact]
    public async Task A_partial_return_reverses_revenue_tax_and_cash_for_what_came_back()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 100m, tax);
        var order = await Json(await SellAsync(t, item, 2, 210m));
        var before = await BalancesAsync(t);

        var ret = await Json(await ReturnAsync(t, order, 1, 105m));
        Assert.StartsWith("POSR", (string)ret["number"]!);
        Assert.Equal((100m, 5m, 105m), ((decimal)ret["subTotal"]!, (decimal)ret["taxTotal"]!, (decimal)ret["totalAmount"]!));

        var after = await BalancesAsync(t);
        Assert.Equal(-105m, Delta(before, after, "1000"));
        Assert.Equal(100m, Delta(before, after, "4100")); // a service item: its income comes back off service revenue
        Assert.Equal(5m, Delta(before, after, "2100"));
        Assert.Equal(100m + 210m - 105m, await ExpectedCashAsync(t));

        var sale = await Json(await t.C.GetAsync($"{Pos}/orders/{(string)order["id"]!}"));
        Assert.Equal("partiallyRefunded", (string)sale["paymentStatus"]!);
        Assert.Equal(105m, (decimal)sale["returnedTotal"]!);
        Assert.Equal(1m, (decimal)sale["lines"]![0]!["returnedQuantity"]!);
    }

    [Fact]
    public async Task You_cannot_return_more_than_was_sold_or_refund_a_different_amount()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 10m);
        var order = await Json(await SellAsync(t, item, 2, 20m));

        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(t, order, 1, 9m)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(t, order, 3, 30m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(t, order, 1, 10m, reason: "  ")).StatusCode);

        (await ReturnAsync(t, order, 2, 20m)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(t, order, 1, 10m)).StatusCode);
        Assert.Equal("refunded", (string)(await Json(await t.C.GetAsync($"{Pos}/orders/{(string)order["id"]!}")))["paymentStatus"]!);
    }

    [Fact]
    public async Task Unit_by_unit_returns_give_back_exactly_what_was_paid_despite_rounding()
    {
        var t = await OpenTillAsync(0m);
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 3.33m, tax);
        // 3 x 3.33 = 9.99, less 10% = 8.99, tax 0.45: total 9.44.
        var order = await Json(await SellAsync(t, item, 3, 9.44m, discount: 10));
        Assert.Equal(9.44m, (decimal)order["totalAmount"]!);

        // Net 8.99 and tax 0.45, shared out as running thirds. The web till works out the same figures (pos-cart.spec.ts).
        var first = await Json(await ReturnAsync(t, order, 1, 3.15m));
        var second = await Json(await ReturnAsync(t, order, 1, 3.14m));
        var third = await Json(await ReturnAsync(t, order, 1, 3.15m));

        Assert.Equal(9.44m, (decimal)first["totalAmount"]! + (decimal)second["totalAmount"]! + (decimal)third["totalAmount"]!);
        Assert.Equal(8.99m, (decimal)first["subTotal"]! + (decimal)second["subTotal"]! + (decimal)third["subTotal"]!);
        Assert.Equal(0m, await ExpectedCashAsync(t));
    }

    [Fact]
    public async Task Very_small_prices_never_give_a_return_negative_tax()
    {
        var t = await OpenTillAsync(1m);
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 0.10m, tax);
        var order = await Json(await SellAsync(t, item, 4, 0.42m)); // net 0.40, tax 0.02
        var before = await BalancesAsync(t);

        foreach (var amount in new[] { 0.11m, 0.10m, 0.11m, 0.10m })
        {
            var ret = await Json(await ReturnAsync(t, order, 1, amount));
            Assert.True((decimal)ret["taxTotal"]! >= 0);
        }

        var after = await BalancesAsync(t);
        Assert.Equal(-0.42m, Delta(before, after, "1000"));
        Assert.Equal(0.02m, Delta(before, after, "2100"));
    }

    [Fact]
    public async Task Goods_put_back_on_the_shelf_return_stock_and_cost_but_damaged_goods_do_not()
    {
        var t = await OpenTillAsync(500m);
        var wh = await Json(await t.C.PostAsJsonAsync("/api/v1/m/inventory/warehouses", new { code = "W" + Guid.NewGuid().ToString("N")[..6], name = "Shelf", legalEntityId = t.Entity }));
        var whId = (string)wh["id"]!;
        (await t.C.PutAsJsonAsync($"{Pos}/registers/{t.Register}", new UpdateRegisterCommand(t.Register, "Till", Guid.Parse(whId), null, true, null, null, null))).EnsureSuccessStatusCode();
        var item = await ItemAsync(t, 50m, type: ItemType.Product);

        var accounts = (await Json(await t.C.GetAsync("/api/v1/finance/accounts?pageSize=300"))).AsArray();
        var ap = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;
        (await t.C.PostAsJsonAsync("/api/v1/m/inventory/stock/receive", new { warehouseId = whId, itemId = item.ToString(), quantity = 10m, unitCost = 15m, date = "2026-03-01", offsetAccountId = ap })).EnsureSuccessStatusCode();

        async Task<(decimal Qty, decimal Value)> StockAsync()
        {
            var row = (await Json(await t.C.GetAsync($"/api/v1/m/inventory/stock?warehouseId={whId}&itemId={item}")))["rows"]!.AsArray().Single()!;
            return ((decimal)row["quantity"]!, (decimal)row["value"]!);
        }

        var catalog = await Json(await t.C.GetAsync($"{Pos}/terminal/catalog?registerId={t.Register}&pageSize=200&search=Till"));
        Assert.Equal(10m, (decimal)catalog["items"]!.AsArray().First(i => (Guid)i!["id"]! == item)!["onHand"]!);

        var order = await Json(await SellAsync(t, item, 3, 150m));
        Assert.Equal((7m, 105m), await StockAsync());
        var before = await BalancesAsync(t);

        (await ReturnAsync(t, order, 1, 50m)).EnsureSuccessStatusCode(); // back on the shelf
        Assert.Equal((8m, 120m), await StockAsync());
        var mid = await BalancesAsync(t);
        Assert.Equal(-15m, Delta(before, mid, "5000")); // cost of sales reversed at the cost it left at
        Assert.Equal(15m, Delta(before, mid, "1300"));

        (await ReturnAsync(t, order, 1, 50m, restock: false, reason: "Damaged")).EnsureSuccessStatusCode();
        Assert.Equal((8m, 120m), await StockAsync());
        var end = await BalancesAsync(t);
        Assert.Equal(0m, Delta(mid, end, "5000")); // the loss stays in cost of sales
        Assert.Equal(50m, Delta(mid, end, "4000"));
    }

    [Fact]
    public async Task A_cash_refund_cannot_take_more_than_the_drawer_holds()
    {
        var t = await OpenTillAsync(0m);
        var item = await ItemAsync(t, 10m);
        var order = await Json(await SellAsync(t, item, 1, 10m, PosPaymentMethod.Card));

        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(t, order, 1, 10m)).StatusCode);
        (await ReturnAsync(t, order, 1, 10m, PosPaymentMethod.Card)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_sale_with_a_return_cannot_be_voided_and_returns_need_their_own_permission()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 10m);
        var order = await Json(await SellAsync(t, item, 2, 20m));
        (await ReturnAsync(t, order, 1, 10m)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await t.C.PostAsJsonAsync($"{Pos}/orders/{(string)order["id"]!}/void", new { reason = "Mistake" })).StatusCode);

        var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate, Permissions.OrdersRead);
        var res = await seller.PostAsJsonAsync($"{Pos}/returns", new CreatePosReturnCommand(Guid.Parse((string)order["id"]!), t.Session, "Try",
            [new PosReturnLineInput(Guid.Parse((string)order["lines"]![0]!["id"]!), 1)], [new PosRefundInput(PosPaymentMethod.Cash, 10m)]));
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    // ---- on account ----

    [Fact]
    public async Task An_on_account_sale_becomes_an_invoice_the_customer_owes_and_counts_as_one_sale()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 100m, tax);
        var customer = await CustomerAsync(t);
        var before = await BalancesAsync(t);

        var noCustomer = await SellAsync(t, item, 1, 105m, PosPaymentMethod.Account);
        Assert.Equal(HttpStatusCode.BadRequest, noCustomer.StatusCode);

        var order = await Json(await SellAsync(t, item, 1, 105m, PosPaymentMethod.Account, Guid.Parse(customer)));
        Assert.Equal("onAccount", (string)order["paymentStatus"]!);
        Assert.False(string.IsNullOrEmpty((string?)order["invoiceNumber"]));

        var after = await BalancesAsync(t);
        Assert.Equal(105m, Delta(before, after, "1100")); // receivable
        Assert.Equal(-5m, Delta(before, after, "2100"));  // tax
        Assert.Equal(0m, Delta(before, after, "1000"));   // no cash
        Assert.Equal(0m, Delta(before, after, "1010"));   // no card
        Assert.Equal(-100m, Delta(before, after, "4100") + Delta(before, after, "4000")); // revenue once

        var aging = await Json(await t.C.GetAsync($"/api/v1/finance/reports/customer-statement?partyId={customer}&asOf=2099-12-31&legalEntityId={t.Entity}"));
        Assert.Equal(105m, (decimal)aging["balance"]!);

        // Void takes it all back, invoice included.
        (await t.C.PostAsJsonAsync($"{Pos}/orders/{(string)order["id"]!}/void", new { reason = "Wrong customer" })).EnsureSuccessStatusCode();
        var voided = await BalancesAsync(t);
        Assert.Equal(0m, Delta(before, voided, "1100"));
        Assert.Equal(0m, Delta(before, voided, "2100"));
        Assert.Equal(0m, Delta(before, voided, "4100") + Delta(before, voided, "4000"));
    }

    [Fact]
    public async Task Returning_an_on_account_sale_to_the_account_issues_a_credit_note()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 100m, tax);
        var customer = await CustomerAsync(t);
        var order = await Json(await SellAsync(t, item, 2, 210m, PosPaymentMethod.Account, Guid.Parse(customer)));
        var before = await BalancesAsync(t);

        var ret = await Json(await ReturnAsync(t, order, 1, 105m, PosPaymentMethod.Account));
        Assert.False(string.IsNullOrEmpty((string?)ret["creditNoteNumber"]));

        var after = await BalancesAsync(t);
        Assert.Equal(-105m, Delta(before, after, "1100"));
        Assert.Equal(5m, Delta(before, after, "2100"));
        Assert.Equal(100m, Delta(before, after, "4100") + Delta(before, after, "4000"));

        var statement = await Json(await t.C.GetAsync($"/api/v1/finance/reports/customer-statement?partyId={customer}&asOf=2099-12-31&legalEntityId={t.Entity}"));
        Assert.Equal(105m, (decimal)statement["balance"]!);
    }

    [Fact]
    public async Task A_sale_paid_part_on_account_comes_back_each_way_and_the_invoice_is_settled_by_the_credit()
    {
        var t = await OpenTillAsync(500m);
        var item = await ItemAsync(t, 100m);
        var customer = await CustomerAsync(t);
        var order = await Json(await t.C.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, Guid.Parse(customer), null,
            [new PosOrderLineInput(item)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 60m, 60m, 0, null), new PosPaymentInput(PosPaymentMethod.Account, 40m, 40m, 0, null)])));

        var detail = await Json(await t.C.GetAsync($"{Pos}/orders/{(string)order["id"]!}"));
        Assert.Equal((60m, 40m), ((decimal)detail["moneyLeftToRefund"]!, (decimal)detail["onAccountLeft"]!));

        var ret = await Json(await t.C.PostAsJsonAsync($"{Pos}/returns", new CreatePosReturnCommand(Guid.Parse((string)order["id"]!), t.Session, "Changed mind",
            [new PosReturnLineInput(Guid.Parse((string)order["lines"]![0]!["id"]!), 1)],
            [new PosRefundInput(PosPaymentMethod.Cash, 60m), new PosRefundInput(PosPaymentMethod.Account, 40m)])));
        Assert.Equal(100m, (decimal)ret["totalAmount"]!);

        var invoice = await Json(await t.C.GetAsync($"/api/v1/finance/invoices/{(string)order["invoiceId"]!}"));
        Assert.Equal(40m, (decimal)invoice["paid"]!); // settled by the credit note, so nothing is left open on ageing
        Assert.Equal("paid", (string)invoice["status"]!);
        var statement = await Json(await t.C.GetAsync($"/api/v1/finance/reports/customer-statement?partyId={customer}&asOf=2099-12-31&legalEntityId={t.Entity}"));
        Assert.Equal(0m, (decimal)statement["balance"]!);
        Assert.Empty(statement["items"]!.AsArray());

        detail = await Json(await t.C.GetAsync($"{Pos}/orders/{(string)order["id"]!}"));
        Assert.Equal((0m, 0m), ((decimal)detail["moneyLeftToRefund"]!, (decimal)detail["onAccountLeft"]!));
    }

    [Fact]
    public async Task A_cashier_cannot_post_a_drawer_movement_to_a_chosen_ledger_account()
    {
        var t = await OpenTillAsync(100m);
        var cashier = await SignInWithRoleAsync(t, Permissions.SessionsManage);
        var ar = (string)(await Json(await t.C.GetAsync("/api/v1/finance/accounts?pageSize=300"))).AsArray().First(a => (string?)a!["code"] == "1100")!["id"]!;
        var res = await cashier.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type = "PayOut", amount = 5m, reason = "Try", accountId = ar });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
        Assert.Equal(100m, await ExpectedCashAsync(t));
        (await cashier.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type = "PayOut", amount = 5m, reason = "Stamps" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task An_on_account_sale_cannot_be_refunded_in_cash_and_a_paid_one_not_beyond_what_was_paid()
    {
        var t = await OpenTillAsync(500m);
        var item = await ItemAsync(t, 100m);
        var customer = Guid.Parse(await CustomerAsync(t));

        var onAccount = await Json(await SellAsync(t, item, 1, 100m, PosPaymentMethod.Account, customer));
        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(t, onAccount, 1, 100m)).StatusCode);
        (await ReturnAsync(t, onAccount, 1, 100m, PosPaymentMethod.Account)).EnsureSuccessStatusCode();

        // Half paid by card, half on account: the card part can come back once, and the rest only to the account.
        var mixed = await Json(await t.C.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, customer, null,
            [new PosOrderLineInput(item, Quantity: 2)],
            [new PosPaymentInput(PosPaymentMethod.Card, 100m, 100m, 0, "A1"), new PosPaymentInput(PosPaymentMethod.Account, 100m, 100m, 0, null)])));
        Assert.Equal("partiallyPaid", (string)mixed["paymentStatus"]!);
        (await ReturnAsync(t, mixed, 1, 100m, PosPaymentMethod.Card)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await ReturnAsync(t, mixed, 1, 100m, PosPaymentMethod.Card)).StatusCode);
        (await ReturnAsync(t, mixed, 1, 100m, PosPaymentMethod.Account)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Selling_on_account_needs_its_permission_and_a_cash_sale_cannot_refund_to_an_account()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 10m);
        var customer = Guid.Parse(await CustomerAsync(t));
        var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate);
        var body = new CreatePosOrderCommand(t.Entity, t.Register, t.Session, customer, null, [new PosOrderLineInput(item)], [new PosPaymentInput(PosPaymentMethod.Account, 10m, 10m, 0, null)]);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.PostAsJsonAsync($"{Pos}/orders", body)).StatusCode);

        var cash = await Json(await SellAsync(t, item, 1, 10m));
        Assert.Equal(HttpStatusCode.BadRequest, (await ReturnAsync(t, cash, 1, 10m, PosPaymentMethod.Account)).StatusCode);
    }

    [Fact]
    public async Task Other_tenders_stay_out_of_the_cash_account_and_a_chosen_customer_names_the_ticket()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 10m);
        var customer = await CustomerAsync(t);
        var before = await BalancesAsync(t);
        var order = await Json(await SellAsync(t, item, 1, 10m, PosPaymentMethod.Other, Guid.Parse(customer)));
        var after = await BalancesAsync(t);
        Assert.Equal(0m, Delta(before, after, "1000"));
        Assert.Equal(10m, Delta(before, after, "1010"));
        Assert.StartsWith("Till customer", (string)order["customerName"]!);

        var vendor = await Json(await t.C.PostAsJsonAsync("/api/v1/parties", new { name = "Vendor " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Vendor" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(t, item, 1, 10m, customer: Guid.Parse((string)vendor["id"]!))).StatusCode);
    }

    // ---- cash in the ledger ----

    [Fact]
    public async Task Pay_outs_and_pay_ins_reach_the_ledger_and_floats_and_safe_drops_stay_inside_cash_on_hand()
    {
        var t = await OpenTillAsync(200m);
        var before = await BalancesAsync(t);
        Task<HttpResponseMessage> Move(string type, decimal amount) => t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type, amount, reason = "Test " + type });

        (await Move("PayOut", 30m)).EnsureSuccessStatusCode();
        var a = await BalancesAsync(t);
        Assert.Equal(-30m, Delta(before, a, "1000"));
        Assert.Equal(30m, Delta(before, a, "6900")); // general expenses

        (await Move("PayIn", 10m)).EnsureSuccessStatusCode();
        var b = await BalancesAsync(t);
        Assert.Equal(10m, Delta(a, b, "1000"));
        Assert.Equal(-10m, Delta(a, b, "6900"));

        (await Move("DropOut", 50m)).EnsureSuccessStatusCode();
        (await Move("FloatIn", 5m)).EnsureSuccessStatusCode();
        var c = await BalancesAsync(t);
        Assert.Equal(0m, Delta(b, c, "1000"));
        Assert.Equal(200m - 30m + 10m - 50m + 5m, await ExpectedCashAsync(t));
    }

    [Fact]
    public async Task A_safe_drop_to_a_named_account_moves_the_money_there()
    {
        var t = await OpenTillAsync(100m);
        var bank = (string)(await Json(await t.C.GetAsync("/api/v1/finance/accounts?pageSize=300"))).AsArray().First(a => (string?)a!["code"] == "1010")!["id"]!;
        var before = await BalancesAsync(t);
        (await t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type = "DropOut", amount = 40m, reason = "Bank deposit", accountId = bank })).EnsureSuccessStatusCode();
        var after = await BalancesAsync(t);
        Assert.Equal(-40m, Delta(before, after, "1000"));
        Assert.Equal(40m, Delta(before, after, "1010"));
    }

    [Fact]
    public async Task The_drawer_cannot_go_below_zero_unless_the_setting_allows_it()
    {
        var t = await OpenTillAsync(20m);
        Task<HttpResponseMessage> PayOut(decimal amount) => t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type = "PayOut", amount, reason = "Supplies" });

        Assert.Equal(HttpStatusCode.Conflict, (await PayOut(25m)).StatusCode);
        Assert.Equal(20m, await ExpectedCashAsync(t));

        Assert.Equal(HttpStatusCode.NoContent, (await t.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["pos.allowNegativeDrawer"] = true })).StatusCode);
        try
        {
            (await PayOut(25m)).EnsureSuccessStatusCode();
            Assert.Equal(-5m, await ExpectedCashAsync(t));
        }
        finally
        {
            await t.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["pos.allowNegativeDrawer"] = false });
        }
    }

    [Theory]
    [InlineData(95, -5)]
    [InlineData(112, 12)]
    [InlineData(100, 0)]
    public async Task Closing_posts_what_is_over_or_short_so_ledger_cash_moves_by_what_was_really_counted(decimal counted, decimal difference)
    {
        var t = await OpenTillAsync(100m);
        var before = await BalancesAsync(t);
        var closed = await Json(await t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/close", new { actualCash = counted }));
        Assert.Equal(difference, (decimal)closed["cashDifference"]!);

        var after = await BalancesAsync(t);
        Assert.Equal(difference, Delta(before, after, "1000"));
        Assert.Equal(-difference, Delta(before, after, "6900"));
    }

    [Fact]
    public async Task After_a_full_shift_ledger_cash_moved_by_exactly_what_the_drawer_gained()
    {
        var t = await OpenTillAsync(100m);
        var item = await ItemAsync(t, 40m);
        var before = await BalancesAsync(t);

        var sale = await Json(await SellAsync(t, item, 2, 80m));
        await Json(await SellAsync(t, item, 1, 40m, PosPaymentMethod.Card));
        var voided = await Json(await SellAsync(t, item, 1, 40m));
        (await t.C.PostAsJsonAsync($"{Pos}/orders/{(string)voided["id"]!}/void", new { reason = "Mistake" })).EnsureSuccessStatusCode();
        (await ReturnAsync(t, sale, 1, 40m)).EnsureSuccessStatusCode();
        (await t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/drawer-transaction", new { type = "PayOut", amount = 5m, reason = "Stamps" })).EnsureSuccessStatusCode();
        (await t.C.PostAsJsonAsync($"{Pos}/sessions/{t.Session}/close", new { actualCash = 133m })).EnsureSuccessStatusCode(); // expected 135, 2 short

        var after = await BalancesAsync(t);
        Assert.Equal(133m - 100m, Delta(before, after, "1000"));
        Assert.Equal(40m, Delta(before, after, "1010"));
    }

    // ---- reports ----

    [Fact]
    public async Task The_shift_report_adds_up_sales_voids_returns_tenders_and_cash()
    {
        var t = await OpenTillAsync(100m);
        var tax = await TaxAsync(t, 5m);
        var item = await ItemAsync(t, 100m, tax);

        var cash = await Json(await SellAsync(t, item, 1, 94.50m, discount: 10)); // 100 - 10 + 4.5
        await Json(await SellAsync(t, item, 1, 105m, PosPaymentMethod.Card));
        var voided = await Json(await SellAsync(t, item, 1, 105m));
        (await t.C.PostAsJsonAsync($"{Pos}/orders/{(string)voided["id"]!}/void", new { reason = "Mistake" })).EnsureSuccessStatusCode();
        (await ReturnAsync(t, cash, 1, 94.50m)).EnsureSuccessStatusCode();
        await t.C.PostAsJsonAsync($"{Pos}/orders/park", new ParkPosOrderCommand(t.Session, null, null, [new PosOrderLineInput(item)]));

        var r = await Json(await t.C.GetAsync($"{Pos}/sessions/{t.Session}/report"));
        Assert.Equal(2, (int)r["saleCount"]!);
        Assert.Equal(1, (int)r["voidCount"]!);
        Assert.Equal(1, (int)r["returnCount"]!);
        Assert.Equal(1, (int)r["heldCount"]!);
        Assert.Equal(200m, (decimal)r["grossSales"]!);
        Assert.Equal(10m, (decimal)r["discounts"]!);
        Assert.Equal(190m, (decimal)r["netSales"]!);
        Assert.Equal(9.5m, (decimal)r["tax"]!);
        Assert.Equal(199.5m, (decimal)r["salesTotal"]!);
        Assert.Equal(94.5m, (decimal)r["returns"]!);
        Assert.Equal(105m, (decimal)r["voids"]!);
        Assert.Equal(105m, (decimal)r["netTakings"]!);
        Assert.Equal(94.5m, (decimal)r["cashSales"]!);
        Assert.Equal(94.5m, (decimal)r["cashRefunds"]!);

        var tenders = r["tenders"]!.AsArray();
        Assert.Equal(0m, (decimal)tenders.First(x => (string)x!["method"]! == "cash")!["net"]!);
        Assert.Equal(105m, (decimal)tenders.First(x => (string)x!["method"]! == "card")!["net"]!);
        Assert.Equal(100m, (decimal)r["session"]!["expectedCash"]!);
    }

    [Fact]
    public async Task The_period_report_covers_todays_trading_by_register_and_rejects_a_backwards_range()
    {
        var t = await OpenTillAsync(100m);
        var item = await ItemAsync(t, 20m);
        var sale = await Json(await SellAsync(t, item, 2, 40m));
        (await ReturnAsync(t, sale, 1, 20m)).EnsureSuccessStatusCode();

        var r = await Json(await t.C.GetAsync($"{Pos}/reports/sales?registerId={t.Register}"));
        Assert.Equal(40m, (decimal)r["salesTotal"]!);
        Assert.Equal(20m, (decimal)r["returns"]!);
        Assert.Equal(20m, (decimal)r["netTakings"]!);
        Assert.Equal(1, (int)r["saleCount"]!);
        Assert.Equal(40m, (decimal)r["days"]![0]!["sales"]!);
        Assert.Equal(20m, (decimal)r["days"]![0]!["net"]!);
        Assert.Single(r["registers"]!.AsArray());
        Assert.Equal(20m, (decimal)r["topItems"]![0]!["quantity"]! * 10m);

        Assert.Equal(HttpStatusCode.BadRequest, (await t.C.GetAsync($"{Pos}/reports/sales?from=2026-02-01&to=2026-01-01")).StatusCode);
    }

    [Fact]
    public async Task Editing_a_register_keeps_accounts_an_older_client_does_not_send()
    {
        var t = await OpenTillAsync();
        var accounts = (await Json(await t.C.GetAsync("/api/v1/finance/accounts?pageSize=300"))).AsArray();
        Guid Acct(string code) => Guid.Parse((string)accounts.First(a => (string?)a!["code"] == code)!["id"]!);

        var set = await Json(await t.C.PutAsJsonAsync($"{Pos}/registers/{t.Register}", new UpdateRegisterCommand(t.Register, "Till", null, null, true, null, null, null, Acct("1010"), Acct("6900"), Acct("6200"))));
        Assert.Equal(Acct("1010"), (Guid)set["cardAccountId"]!);

        var old = await Json(await t.C.PutAsJsonAsync($"{Pos}/registers/{t.Register}", new { name = "Till 2", isActive = true }));
        Assert.Equal(Acct("6200"), (Guid)old["expenseAccountId"]!);

        var cleared = await Json(await t.C.PutAsJsonAsync($"{Pos}/registers/{t.Register}", new UpdateRegisterCommand(t.Register, "Till 2", null, null, true, null, null, null, Guid.Empty)));
        Assert.Null(cleared["cardAccountId"]);

        var bad = await t.C.PutAsJsonAsync($"{Pos}/registers/{t.Register}", new UpdateRegisterCommand(t.Register, "Till 2", null, Guid.NewGuid(), true, null, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode); // no such account
    }

    [Fact]
    public async Task A_duplicate_register_code_is_a_conflict()
    {
        var t = await OpenTillAsync();
        var code = "D-" + Guid.NewGuid().ToString("N")[..6];
        (await t.C.PostAsJsonAsync($"{Pos}/registers", new { legalEntityId = t.Entity, code, name = "One" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await t.C.PostAsJsonAsync($"{Pos}/registers", new { legalEntityId = t.Entity, code, name = "Two" })).StatusCode);
    }
}
