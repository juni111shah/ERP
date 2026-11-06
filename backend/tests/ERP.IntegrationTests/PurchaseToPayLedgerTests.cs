using System.Net;
using System.Net.Http.Json;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Warehouses, part billing, three-way control and price variance on the procure-to-pay path, asserted on the ledger and the stock ledger.</summary>
public class PurchaseToPayLedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Clearing = "inventory.clearing";
    private const string Ap = "ap.control";
    private const string Stock = "inventory";
    private const string Expense = "purchases.expense";
    private const string Variance = "purchase.variance";

    // ---- warehouses (P1)

    [Fact]
    public async Task A_legal_entity_with_no_warehouse_cannot_receive_into_another_entitys_warehouse()
    {
        var k = await NewAsync(f); // this entity has a warehouse
        var bare = await NewEntityAsync(k.C, "Bare " + Guid.NewGuid().ToString("N")[..6]);
        var (po, line) = await k.ApprovedOrderAsync(entity: bare);

        var res = await k.ReceiveAsync(po, line, 5m, entity: bare);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("warehouse", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, (await k.StockAsync()).Quantity); // nothing landed in the other entity's warehouse
        Assert.Equal(0m, await k.BalanceAsync(Stock));
        Assert.Equal(0m, await k.BalanceAsync(Stock, bare));
        Assert.Equal(0m, await k.BalanceAsync(Clearing, bare));
        Assert.Equal("approved", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task A_warehouse_of_another_entity_or_an_unknown_one_is_an_error_not_a_fallback()
    {
        var k = await NewAsync(f);
        var other = await NewEntityAsync(k.C, "Other " + Guid.NewGuid().ToString("N")[..6]);
        var foreign = await NewWarehouseAsync(k.C, other);
        var (po, line) = await k.ApprovedOrderAsync();

        foreach (var wrong in new[] { foreign, Guid.NewGuid().ToString() })
        {
            var res = await k.ReceiveAsync(po, line, 5m, warehouseId: wrong);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            Assert.Contains("warehouseId", await res.Content.ReadAsStringAsync());
        }
        Assert.Equal(0m, (await k.StockAsync()).Quantity);
        Assert.Equal(0m, await k.BalanceAsync(Stock));
    }

    [Fact]
    public async Task A_receipt_lands_in_the_warehouse_it_names_and_the_legal_entity_books_it()
    {
        var k = await NewAsync(f);
        var second = await NewWarehouseAsync(k.C, k.Entity);
        var (po, line) = await k.ApprovedOrderAsync();

        var grn = await k.ReceivedAsync(po, line, 4m, warehouseId: second);

        Assert.Equal(second, (string)grn["warehouseId"]!);
        Assert.Equal(4m, (await k.StockAsync(second)).Quantity);
        Assert.Equal(0m, (await k.StockAsync(k.Warehouse)).Quantity);
        Assert.Equal(400m, await k.BalanceAsync(Stock));
        Assert.Equal(-400m, await k.BalanceAsync(Clearing));
    }

    // ---- part billing and three-way control (P3)

    [Fact]
    public async Task An_order_can_be_billed_in_parts_for_what_was_received_and_clearing_ends_at_zero()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync(vendorReference: "VR-" + Guid.NewGuid().ToString("N")[..6]);

        await k.ReceivedAsync(po, line, 4m);
        var first = await k.BillAsync(po);
        Assert.Equal(400m, (decimal)first["total"]!);
        Assert.Equal(4m, (decimal)first["lines"]![0]!["quantity"]!);
        await k.PostBillAsync(first);

        var afterFirst = await k.OrderAsync(po);
        Assert.Equal("partiallyReceived", (string)afterFirst["status"]!, ignoreCase: true);
        Assert.Equal(4m, (decimal)afterFirst["lines"]![0]!["billedQuantity"]!);
        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(-400m, await k.BalanceAsync(Ap));

        // everything received is billed: there is nothing for a second bill yet
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsync($"{Pur}/orders/{po}/convert-to-bill", null)).StatusCode);

        await k.ReceivedAsync(po, line, 6m);
        var second = await k.BillAsync(po);
        Assert.Equal(600m, (decimal)second["total"]!);
        Assert.NotEqual((string)first["id"]!, (string)second["id"]!);
        Assert.NotEqual((string)first["vendorReference"]!, (string)second["vendorReference"]!);
        await k.PostBillAsync(second);

        var done = await k.OrderAsync(po);
        Assert.Equal("billed", (string)done["status"]!, ignoreCase: true);
        Assert.Equal((string)second["id"]!, (string)done["invoicedBillId"]!);
        Assert.Equal(2, done["billIds"]!.AsArray().Count);
        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(-1000m, await k.BalanceAsync(Ap));
        Assert.Equal(1000m, await k.BalanceAsync(Stock));
        Assert.Equal(0m, await k.BalanceAsync(Expense));
    }

    [Fact]
    public async Task Billing_more_than_received_is_refused_unless_the_caller_opts_in()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 4m);

        var over = await k.C.PostAsJsonAsync($"{Pur}/orders/{po}/convert-to-bill", new { lines = new[] { new { lineId = line, quantity = 5m } } });
        Assert.Equal(HttpStatusCode.Conflict, over.StatusCode);
        Assert.Contains("allowUnreceived", await over.Content.ReadAsStringAsync());
        Assert.Null((await k.OrderAsync(po))["invoicedBillId"]);

        var unknownLine = await k.C.PostAsJsonAsync($"{Pur}/orders/{po}/convert-to-bill", new { lines = new[] { new { lineId = Guid.NewGuid(), quantity = 1m } } });
        Assert.Equal(HttpStatusCode.BadRequest, unknownLine.StatusCode);
    }

    [Fact]
    public async Task Goods_billed_ahead_of_receipt_wait_on_clearing_and_can_still_be_received()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();

        var bill = await k.BillAsync(po, new { allowUnreceived = true });
        Assert.Equal(1000m, (decimal)bill["total"]!);
        await k.PostBillAsync(bill);

        // not expensed: the debit waits on clearing, and the order is still open for receipts
        Assert.Equal(0m, await k.BalanceAsync(Expense));
        Assert.Equal(1000m, await k.BalanceAsync(Clearing));
        Assert.Equal(-1000m, await k.BalanceAsync(Ap));
        Assert.Equal("approved", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);

        await k.ReceivedAsync(po, line, 10m);

        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(1000m, await k.BalanceAsync(Stock));
        Assert.Equal((10m, 1000m), await k.StockAsync());
        Assert.Equal("billed", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);
    }

    [Fact]
    public async Task Voiding_one_of_two_bills_releases_only_its_quantity_and_its_ledger_effect()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);
        var first = await k.BillAsync(po, new { lines = new[] { new { lineId = line, quantity = 4m } } });
        await k.PostBillAsync(first);
        var second = await k.BillAsync(po);
        Assert.Equal(600m, (decimal)second["total"]!);
        await k.PostBillAsync(second);
        Assert.Equal("billed", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);

        await Ok(await k.C.PostAsJsonAsync($"{Fin}/bills/{(string)second["id"]!}/void", new { reason = "Wrong vendor invoice" }));

        var order = await k.OrderAsync(po);
        Assert.Equal("received", (string)order["status"]!, ignoreCase: true);
        Assert.Equal(4m, (decimal)order["lines"]![0]!["billedQuantity"]!);
        Assert.Equal((string)first["id"]!, (string)order["invoicedBillId"]!);
        Assert.Equal(-400m, await k.BalanceAsync(Ap));
        Assert.Equal(-600m, await k.BalanceAsync(Clearing));

        var again = await k.BillAsync(po);
        Assert.Equal(600m, (decimal)again["total"]!);
    }

    [Fact]
    public async Task A_draft_bill_edited_above_its_order_quantity_cannot_be_posted()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 4m);
        var bill = await k.BillAsync(po);
        var l = bill["lines"]![0]!;

        await Ok(await k.C.PutAsJsonAsync($"{Fin}/bills/{(string)bill["id"]!}", new
        {
            legalEntityId = k.Entity, partyId = k.Vendor, billDate = (string)bill["billDate"]!,
            lines = new[] { new { description = (string)l["description"]!, quantity = 9m, price = 100m, accountId = (string)l["accountId"]! } },
        }));

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsync($"{Fin}/bills/{(string)bill["id"]!}/post", null)).StatusCode);
        Assert.Equal(0m, await k.BalanceAsync(Ap));
    }

    // ---- price variance (P4)

    [Fact]
    public async Task A_vendor_price_below_the_order_price_posts_a_credit_to_variance_and_clears_goods_received()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);

        var bill = await k.BillAsync(po, new { unitPriceOverrides = new Dictionary<string, decimal> { [line] = 90m } });
        Assert.Equal(900m, (decimal)bill["total"]!);
        await k.PostBillAsync(bill);

        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(-100m, await k.BalanceAsync(Variance));
        Assert.Equal(-900m, await k.BalanceAsync(Ap));
        Assert.Equal(1000m, await k.BalanceAsync(Stock));
    }

    [Fact]
    public async Task A_lower_vendor_price_on_a_taxed_line_takes_the_tax_down_with_it()
    {
        var k = await NewAsync(f);
        await k.NewTaxCodeAsync(7m);
        var (po, line) = await k.ApprovedOrderAsync(taxRate: 7m);
        await k.ReceivedAsync(po, line, 10m);

        var bill = await k.BillAsync(po, new { unitPriceOverrides = new Dictionary<string, decimal> { [line] = 90m } });
        Assert.Equal(63m, (decimal)bill["tax"]!);
        Assert.Equal(963m, (decimal)bill["total"]!);
        await k.PostBillAsync(bill);

        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(-100m, await k.BalanceAsync(Variance));
        Assert.Equal(63m, await k.BalanceAsync("tax.input"));
        Assert.Equal(-963m, await k.BalanceAsync(Ap));
    }

    [Fact]
    public async Task A_vendor_price_above_the_order_price_debits_variance()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);

        await k.PostBillAsync(await k.BillAsync(po, new { unitPriceOverrides = new Dictionary<string, decimal> { [line] = 110m } }));

        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(100m, await k.BalanceAsync(Variance));
        Assert.Equal(-1100m, await k.BalanceAsync(Ap));
    }

    [Fact]
    public async Task A_negative_price_is_refused_on_any_line_but_variance()
    {
        var k = await NewAsync(f);
        var expense = (string)(await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["systemRole"] == Expense)!["id"]!;
        var res = await k.C.PostAsJsonAsync($"{Fin}/bills", new
        {
            legalEntityId = k.Entity, partyId = k.Vendor, billDate = "2026-06-01",
            lines = new[] { new { description = "Supplies", quantity = 1m, price = 100m, accountId = expense }, new { description = "Rebate", quantity = 1m, price = -10m, accountId = expense } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("negative", await res.Content.ReadAsStringAsync());
    }

    // ---- status (P8)

    [Theory]
    [InlineData("Received")]
    [InlineData("PartiallyReceived")]
    public async Task An_order_cannot_be_marked_received_by_hand(string status)
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Pur}/orders/{po}/status", new { status })).StatusCode);

        Assert.Equal("approved", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);
        await k.ReceivedAsync(po, line, 10m); // still receivable
        Assert.Equal("received", (string)(await k.OrderAsync(po))["status"]!, ignoreCase: true);
        Assert.Equal(1000m, await k.BalanceAsync(Stock));
    }
}

/// <summary>Inventory off: a receipt moves no stock, so its bill must leave nothing on goods-received clearing (P2). Runs on its own host because the module switch is workspace-wide.</summary>
public class PurchaseWithoutInventoryTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_receipt_that_moves_no_stock_bills_to_expense_and_leaves_no_clearing_residue()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        (await k.C.PostAsync("/api/v1/modules/inventory/disable", null)).EnsureSuccessStatusCode();
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var off = new SupplyChainKit(c, k.Entity, k.Vendor, k.Customer, k.Warehouse, k.Item, k.Sku);

        await off.ReceivedAsync(po, line, 10m);
        Assert.Equal(0m, await off.BalanceAsync("inventory.clearing")); // nothing was credited

        var bill = await off.BillAsync(po);
        await off.PostBillAsync(bill);

        Assert.Equal(0m, await off.BalanceAsync("inventory.clearing"));
        Assert.Equal(1000m, await off.BalanceAsync("purchases.expense"));
        Assert.Equal(-1000m, await off.BalanceAsync("ap.control"));
        Assert.Equal(0m, await off.BalanceAsync("inventory"));
        Assert.Equal("billed", (string)(await off.OrderAsync(po))["status"]!, ignoreCase: true);
    }
}
