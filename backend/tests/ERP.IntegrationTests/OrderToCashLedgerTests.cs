using System.Net;
using System.Net.Http.Json;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Stock guard, delivery rules, order cancellation, and invoice and receipt voids on the order-to-cash path, asserted on the ledger and the stock ledger.</summary>
public class OrderToCashLedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Stock = "inventory";
    private const string Cogs = "cogs";
    private const string Ar = "ar.control";

    private static string Today => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

    // ---- negative stock (S1)

    [Fact]
    public async Task A_delivery_beyond_stock_on_hand_is_refused_and_posts_nothing()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(3m, 100m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);

        var res = await k.DeliverAsync(so, line, 5m);

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("in stock", await res.Content.ReadAsStringAsync());
        Assert.Equal((3m, 300m), await k.StockAsync());
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
        var order = await k.SaleAsync(so);
        Assert.Equal(0m, (decimal)order["lines"]![0]!["deliveredQuantity"]!);
        Assert.Equal("confirmed", (string)order["status"]!, ignoreCase: true);

        await Ok(await k.DeliverAsync(so, line, 3m)); // what is there can go
        Assert.Equal(300m, await k.BalanceAsync(Cogs));
        Assert.Equal(0m, (await k.StockAsync()).Quantity);
    }

    [Fact]
    public async Task Invoicing_an_order_cannot_issue_stock_that_is_not_there()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(3m, 100m);
        var (so, _) = await k.ConfirmedSaleAsync(quantity: 5m);

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null)).StatusCode);

        Assert.Null((await k.SaleAsync(so))["invoicedInvoiceId"]);
        Assert.Equal((3m, 300m), await k.StockAsync());
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
    }

    [Fact]
    public async Task A_delivery_with_no_warehouse_in_the_legal_entity_is_refused()
    {
        var k = await NewAsync(f, withWarehouse: false);
        var (so, line) = await k.ConfirmedSaleAsync();
        var res = await k.DeliverAsync(so, line, 1m);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("warehouse", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, (decimal)(await k.SaleAsync(so))["lines"]![0]!["deliveredQuantity"]!);
    }

    // ---- delivery rules (delivery date, warehouse, S5)

    [Fact]
    public async Task A_delivery_books_on_its_date_from_the_warehouse_it_names()
    {
        var k = await NewAsync(f);
        var second = await NewWarehouseAsync(k.C, k.Entity);
        await k.OpeningStockAsync(5m, 100m, second);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 2m);

        await Ok(await k.DeliverAsync(so, line, 2m, date: "2026-06-15", warehouseId: second));

        Assert.Equal(3m, (await k.StockAsync(second)).Quantity);
        var moves = (await Ok(await k.C.GetAsync($"{Inv}/movements?pageSize=50&filter=itemId:eq:{k.Item}"))).AsArray();
        var issue = moves.Single(m => string.Equals((string?)m!["kind"], "Issue", StringComparison.OrdinalIgnoreCase))!;
        Assert.Equal("2026-06-15", (string)issue["date"]!);
        Assert.Equal(second, (string)issue["warehouseId"]!);
        Assert.Equal(200m, await k.BalanceAsync(Cogs));
        var june14 = await Ok(await k.C.GetAsync($"{Fin}/reports/trial-balance?to=2026-06-14&legalEntityId={k.Entity}"));
        Assert.DoesNotContain(june14["rows"]!.AsArray(), r => (string?)r!["name"] == "Cost of goods sold" && (decimal)r!["closing"]! != 0m);
    }

    [Fact]
    public async Task A_delivery_date_before_the_stock_arrived_is_refused()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(5m, 100m, date: "2026-06-10");
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 2m);
        Assert.Equal(HttpStatusCode.Conflict, (await k.DeliverAsync(so, line, 2m, date: "2026-06-09")).StatusCode);
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
    }

    [Fact]
    public async Task A_delivery_with_no_date_is_dated_today()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(5m, 100m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 2m);
        await Ok(await k.DeliverAsync(so, line, 2m));
        var moves = (await Ok(await k.C.GetAsync($"{Inv}/movements?pageSize=50&filter=itemId:eq:{k.Item}"))).AsArray();
        Assert.Equal(Today, (string)moves.Single(m => string.Equals((string?)m!["kind"], "Issue", StringComparison.OrdinalIgnoreCase))!["date"]!);
    }

    [Fact]
    public async Task Over_delivery_and_unknown_lines_are_errors_and_move_nothing()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 100m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);

        Assert.Equal(HttpStatusCode.BadRequest, (await k.DeliverAsync(so, line, 6m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.DeliverAsync(so, Guid.NewGuid().ToString(), 1m)).StatusCode);

        Assert.Equal(0m, (decimal)(await k.SaleAsync(so))["lines"]![0]!["deliveredQuantity"]!);
        Assert.Equal((10m, 1000m), await k.StockAsync());
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
    }

    // ---- cancel and delete after delivery

    [Fact]
    public async Task An_order_with_delivered_goods_cannot_be_cancelled_or_deleted()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 100m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);
        await Ok(await k.DeliverAsync(so, line, 2m));

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PutAsJsonAsync($"{Sales}/orders/{so}/status", new { status = "Cancelled" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.DeleteAsync($"{Sales}/orders/{so}")).StatusCode);

        Assert.Equal("processing", (string)(await k.SaleAsync(so))["status"]!, ignoreCase: true);
        Assert.Equal(200m, await k.BalanceAsync(Cogs));
        Assert.Equal(8m, (await k.StockAsync()).Quantity);
    }

    // ---- convert then delete or void the invoice (S4)

    [Fact]
    public async Task Deleting_the_draft_invoice_puts_auto_delivered_goods_back_and_reverses_their_cost()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var (so, _) = await k.ConfirmedSaleAsync(quantity: 5m);

        var draft = await Ok(await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null));
        Assert.Equal(250m, await k.BalanceAsync(Cogs));
        Assert.Equal(5m, (await k.StockAsync()).Quantity);

        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{Fin}/invoices/{(string)draft["id"]!}")).StatusCode);

        Assert.Equal(0m, await k.BalanceAsync(Cogs));
        Assert.Equal((10m, 500m), await k.StockAsync());
        var order = await k.SaleAsync(so);
        Assert.Equal("confirmed", (string)order["status"]!, ignoreCase: true);
        Assert.Equal(0m, (decimal)order["lines"]![0]!["deliveredQuantity"]!);
        Assert.Null(order["invoicedInvoiceId"]);

        await Ok(await k.C.PutAsJsonAsync($"{Sales}/orders/{so}/status", new { status = "Cancelled" })); // no longer stuck
    }

    [Fact]
    public async Task Goods_delivered_before_the_conversion_stay_delivered_when_the_invoice_is_voided()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);
        await Ok(await k.DeliverAsync(so, line, 2m));

        var invoice = await Ok(await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null));
        var invoiceId = (string)invoice["id"]!;
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{invoiceId}/issue", null));
        Assert.Equal(1000m, await k.BalanceAsync(Ar));
        Assert.Equal(250m, await k.BalanceAsync(Cogs));

        await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoiceId}/void", new { reason = "Wrong customer" }));

        Assert.Equal(0m, await k.BalanceAsync(Ar));
        Assert.Equal(100m, await k.BalanceAsync(Cogs)); // the two delivered first
        Assert.Equal((8m, 400m), await k.StockAsync());
        var order = await k.SaleAsync(so);
        Assert.Equal("processing", (string)order["status"]!, ignoreCase: true);
        Assert.Equal(2m, (decimal)order["lines"]![0]!["deliveredQuantity"]!);

        // and it can be invoiced again without issuing the delivered two twice
        await Ok(await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null));
        Assert.Equal(250m, await k.BalanceAsync(Cogs));
        Assert.Equal(5m, (await k.StockAsync()).Quantity);
    }

    // ---- void guards (S2, S3)

    private static async Task<string> IssuedInvoiceAsync(SupplyChainKit k, decimal price = 100m)
    {
        var inv = await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = k.Entity, partyId = k.Customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Service", quantity = 1m, price } } }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{(string)inv["id"]!}/issue", null));
        return (string)inv["id"]!;
    }

    [Fact]
    public async Task An_invoice_with_an_issued_unapplied_credit_note_cannot_be_voided()
    {
        var k = await NewAsync(f);
        var invoice = await IssuedInvoiceAsync(k);
        var note = await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/credit-note", new { }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{(string)note["id"]!}/issue", null));
        Assert.Equal(0m, await k.BalanceAsync(Ar));

        var res = await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Entered twice" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("credit note", await res.Content.ReadAsStringAsync());
        Assert.Equal(0m, await k.BalanceAsync(Ar)); // not a phantom -100
        Assert.Equal("issued", (string)(await Ok(await k.C.GetAsync($"{Fin}/invoices/{invoice}")))["status"]!);

        // void the note first, then the invoice can go
        await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{(string)note["id"]!}/void", new { reason = "Not needed" }));
        await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Entered twice" }));
        Assert.Equal(0m, await k.BalanceAsync(Ar));
    }

    [Fact]
    public async Task A_receipt_from_the_cheque_register_cannot_be_voided_and_keeps_its_ledger_effect()
    {
        var k = await NewAsync(f);
        var cheque = await Ok(await k.C.PostAsJsonAsync($"{Fin}/cheques", new
        {
            legalEntityId = k.Entity, partyId = k.Customer, kind = "cheque", instrumentNo = "C" + Guid.NewGuid().ToString("N")[..8], bankName = "Bank", amount = 500m, instrumentDate = "2026-06-01", receivedOn = "2026-06-01",
        }), HttpStatusCode.Created);
        var settlement = (string)cheque["settlementId"]!;
        Assert.Equal(-500m, await k.BalanceAsync(Ar));

        var res = await k.C.PostAsJsonAsync($"{Fin}/receipts/{settlement}/void", new { reason = "Mistake" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("cheque", await res.Content.ReadAsStringAsync());
        Assert.Equal(-500m, await k.BalanceAsync(Ar));
    }

    [Fact]
    public async Task A_payment_id_is_not_a_receipt()
    {
        var k = await NewAsync(f);
        var bank = (string)(await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["subcategory"] == "Bank" || (string?)a!["code"] == "1010")!["id"]!;
        var payment = await Ok(await k.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = k.Entity, partyId = k.Vendor, date = "2026-06-01", method = "BankTransfer", bankAccountId = bank, amount = 75m }), HttpStatusCode.Created);
        var id = (string)payment["id"]!;

        Assert.Equal(HttpStatusCode.NotFound, (await k.C.GetAsync($"{Fin}/receipts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await k.C.PostAsJsonAsync($"{Fin}/receipts/{id}/void", new { reason = "Mistake" })).StatusCode);
        Assert.Equal(75m, await k.BalanceAsync("ap.control")); // the payment is untouched
    }

    // ---- stock versus ledger

    [Fact]
    public async Task The_reconciliation_report_ties_stock_to_the_inventory_account_and_shows_a_difference()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 4m);
        await Ok(await k.DeliverAsync(so, line, 4m));

        var report = await Ok(await k.C.GetAsync($"{Inv}/reports/stock-gl-reconciliation?legalEntityId={k.Entity}"));
        var row = report["rows"]!.AsArray().Single()!;
        Assert.Equal(k.Entity, (string)row["legalEntityId"]!);
        Assert.Equal(300m, (decimal)row["stockValue"]!);
        Assert.Equal(300m, (decimal)row["inventoryAccountBalance"]!);
        Assert.Equal(0m, (decimal)row["difference"]!);
        Assert.True((bool)report["reconciled"]!);

        // a manual journal on the inventory account has no stock behind it
        var accounts = (await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray();
        var inventory = (string)accounts.First(a => (string?)a!["systemRole"] == "inventory")!["id"]!;
        var cogs = (string)accounts.First(a => (string?)a!["systemRole"] == "cogs")!["id"]!;
        var today = Today;
        var journal = await Ok(await k.C.PostAsJsonAsync($"{Fin}/journals", new
        {
            legalEntityId = k.Entity, date = today, memo = "Adjustment",
            lines = new[] { new { accountId = inventory, debit = 25m, credit = 0m }, new { accountId = cogs, debit = 0m, credit = 25m } },
        }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsync($"{Fin}/journals/{(string)journal["id"]!}/post", null));

        var after = await Ok(await k.C.GetAsync($"{Inv}/reports/stock-gl-reconciliation?legalEntityId={k.Entity}"));
        Assert.Equal(-25m, (decimal)after["rows"]![0]!["difference"]!);
        Assert.False((bool)after["reconciled"]!);
    }

    [Fact]
    public async Task The_reconciliation_report_needs_its_permission()
    {
        var k = await NewAsync(f);
        var role = (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/roles", new { name = "StockOnly " + Guid.NewGuid().ToString("N")[..6], description = "x", permissions = new[] { "inventory.stock.read" } }), HttpStatusCode.Created))["id"]!;
        var email = "st" + Guid.NewGuid().ToString("N")[..10] + "@demo.local";
        await Ok(await k.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Stock Reader", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { role } }), HttpStatusCode.Created);
        var (reader, _) = await f.SignInAsync(email, "Temp#Passw0rd1");

        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync($"{Inv}/reports/stock-gl-reconciliation")).StatusCode);
    }
}

/// <summary>The workspace setting that lifts the document-driven stock guard. On its own host because it applies to the whole workspace.</summary>
public class AllowNegativeStockSettingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task With_the_setting_on_a_delivery_may_go_below_zero()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(1m, 100m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 3m);
        Assert.Equal(HttpStatusCode.Conflict, (await k.DeliverAsync(so, line, 3m)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await k.C.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["inventory.allowNegativeStock"] = true })).StatusCode);

        await Ok(await k.DeliverAsync(so, line, 3m));
        Assert.Equal(3m, (decimal)(await k.SaleAsync(so))["lines"]![0]!["deliveredQuantity"]!);
        var moves = (await Ok(await k.C.GetAsync($"{Inv}/movements?pageSize=50&filter=itemId:eq:{k.Item}"))).AsArray();
        Assert.Equal(-2m, moves.Sum(m => (decimal)m!["quantity"]!));
    }
}
