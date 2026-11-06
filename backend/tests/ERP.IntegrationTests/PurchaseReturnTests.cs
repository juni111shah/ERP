using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Purchase returns and debit notes: a numbered document that takes goods out of stock and reduces what is owed, with a void that undoes it.</summary>
public class PurchaseReturnTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Clearing = "inventory.clearing";
    private const string Ap = "ap.control";
    private const string Stock = "inventory";
    private const string Variance = "purchase.variance";

    private static async Task<JsonNode> DraftAsync(SupplyChainKit k, string? order, string? bill, object[] lines, string date = "2026-07-01")
        => await Ok(await k.C.PostAsJsonAsync($"{Pur}/returns", new { purchaseOrderId = order, billId = bill, returnDate = date, reason = "Faulty goods", lines }), HttpStatusCode.Created);

    private static async Task<JsonNode> PostAsync(SupplyChainKit k, JsonNode ret) => await Ok(await k.C.PostAsync($"{Pur}/returns/{(string)ret["id"]!}/post", null));

    private async Task<(SupplyChainKit K, string Order, string Line, JsonNode Bill)> BilledOrderAsync(decimal taxRate = 0m)
    {
        var k = await NewAsync(f);
        if (taxRate > 0) await k.NewTaxCodeAsync(taxRate);
        var (po, line) = await k.ApprovedOrderAsync(taxRate: taxRate);
        await k.ReceivedAsync(po, line, 10m);
        var bill = await k.PostBillAsync(await k.BillAsync(po));
        return (k, po, line, bill);
    }

    [Fact]
    public async Task Returning_billed_goods_takes_them_out_of_stock_and_reduces_payables_and_the_bill()
    {
        var (k, po, line, bill) = await BilledOrderAsync();
        Assert.Equal(-1000m, await k.BalanceAsync(Ap));

        var draft = await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 3m }]);
        Assert.StartsWith("PRT-", (string)draft["number"]!);
        Assert.Equal((10m, 1000m), await k.StockAsync()); // a draft does nothing

        var posted = await PostAsync(k, draft);

        Assert.Equal("posted", (string)posted["status"]!, ignoreCase: true);
        Assert.Equal(300m, (decimal)posted["totalAmount"]!);
        Assert.NotNull(posted["journalEntryId"]);
        Assert.Equal((7m, 700m), await k.StockAsync());
        Assert.Equal(700m, await k.BalanceAsync(Stock));
        Assert.Equal(-700m, await k.BalanceAsync(Ap));
        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(0m, await k.BalanceAsync(Variance));

        var after = await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)bill["id"]!}"));
        Assert.Equal(300m, (decimal)after["paid"]!);
        Assert.Equal(700m, (decimal)after["outstanding"]!);
        Assert.Equal("partlyPaid", (string)after["status"]!, ignoreCase: true);

        var order = await k.OrderAsync(po);
        Assert.Equal(7m, (decimal)order["lines"]![0]!["receivedQuantity"]!);
        Assert.Equal(7m, (decimal)order["lines"]![0]!["billedQuantity"]!);
        Assert.Equal(3m, (decimal)order["lines"]![0]!["returnedQuantity"]!);
        Assert.Equal("partiallyReceived", (string)order["status"]!, ignoreCase: true); // a replacement can be received

        // the bill cannot be voided from under the return
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Fin}/bills/{(string)bill["id"]!}/void", new { reason = "Wrong" })).StatusCode);
    }

    [Fact]
    public async Task Returning_goods_not_billed_yet_only_comes_off_clearing()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);
        Assert.Equal(-1000m, await k.BalanceAsync(Clearing));

        var posted = await PostAsync(k, await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 4m }]));

        Assert.Equal(0m, (decimal)posted["totalAmount"]!);
        Assert.Equal(400m, (decimal)posted["stockValue"]!);
        Assert.Equal(-600m, await k.BalanceAsync(Clearing));
        Assert.Equal(600m, await k.BalanceAsync(Stock));
        Assert.Equal(0m, await k.BalanceAsync(Ap));
        Assert.Equal((6m, 600m), await k.StockAsync());

        // billing now covers only what is left, and clearing ends at zero
        var bill = await k.BillAsync(po);
        Assert.Equal(600m, (decimal)bill["total"]!);
        await k.PostBillAsync(bill);
        Assert.Equal(0m, await k.BalanceAsync(Clearing));
        Assert.Equal(-600m, await k.BalanceAsync(Ap));
    }

    [Fact]
    public async Task A_return_reverses_recoverable_input_tax_with_the_payable()
    {
        var (k, po, line, _) = await BilledOrderAsync(taxRate: 7m);
        Assert.Equal(70m, await k.BalanceAsync("tax.input"));
        Assert.Equal(-1070m, await k.BalanceAsync(Ap));

        var posted = await PostAsync(k, await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 2m }]));

        Assert.Equal(214m, (decimal)posted["totalAmount"]!);
        Assert.Equal(14m, (decimal)posted["taxTotal"]!);
        Assert.Equal(56m, await k.BalanceAsync("tax.input"));
        Assert.Equal(-856m, await k.BalanceAsync(Ap));
        Assert.Equal(800m, await k.BalanceAsync(Stock));
    }

    [Fact]
    public async Task A_gap_between_stock_cost_and_the_billed_price_goes_to_variance()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);
        await k.PostBillAsync(await k.BillAsync(po, new { unitPriceOverrides = new Dictionary<string, decimal> { [line] = 90m } }));
        Assert.Equal(-100m, await k.BalanceAsync(Variance));

        await PostAsync(k, await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 1m }]));

        Assert.Equal(-810m, await k.BalanceAsync(Ap));
        Assert.Equal(900m, await k.BalanceAsync(Stock));
        Assert.Equal(-90m, await k.BalanceAsync(Variance));
        Assert.Equal(0m, await k.BalanceAsync(Clearing));
    }

    [Fact]
    public async Task A_return_against_a_named_bill_takes_from_that_bill()
    {
        var k = await NewAsync(f);
        var (po, line) = await k.ApprovedOrderAsync();
        await k.ReceivedAsync(po, line, 10m);
        var first = await k.PostBillAsync(await k.BillAsync(po, new { lines = new[] { new { lineId = line, quantity = 4m } } }));
        var second = await k.PostBillAsync(await k.BillAsync(po));

        var posted = await PostAsync(k, await DraftAsync(k, null, (string)first["id"]!, [new { orderLineId = line, quantity = 2m }]));

        Assert.Equal(po, (string)posted["purchaseOrderId"]!);
        Assert.Equal(200m, (decimal)(await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)first["id"]!}")))["paid"]!);
        Assert.Equal(0m, (decimal)(await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)second["id"]!}")))["paid"]!);

        // only what is left on that bill can come off it
        var tooMany = await DraftAsync(k, null, (string)first["id"]!, [new { orderLineId = line, quantity = 3m }]);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsync($"{Pur}/returns/{(string)tooMany["id"]!}/post", null)).StatusCode);
        Assert.Equal(-800m, await k.BalanceAsync(Ap));
    }

    [Fact]
    public async Task A_debit_note_on_a_manual_bill_reduces_payables_and_credits_what_the_bill_debited()
    {
        var k = await NewAsync(f);
        var expense = (string)(await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["systemRole"] == "purchases.expense")!["id"]!;
        var bill = await Ok(await k.C.PostAsJsonAsync($"{Fin}/bills", new
        {
            legalEntityId = k.Entity, partyId = k.Vendor, billDate = "2026-06-01",
            lines = new[] { new { description = "Consumables", quantity = 10m, price = 20m, accountId = expense } },
        }), HttpStatusCode.Created);
        var posted = await k.PostBillAsync(bill);
        var billLine = (string)posted["lines"]![0]!["id"]!;

        await PostAsync(k, await DraftAsync(k, null, (string)posted["id"]!, [new { billLineId = billLine, quantity = 3m }]));

        Assert.Equal(-140m, await k.BalanceAsync(Ap));
        Assert.Equal(140m, await k.BalanceAsync("purchases.expense"));
        Assert.Equal(60m, (decimal)(await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)posted["id"]!}")))["paid"]!);

        var over = await DraftAsync(k, null, (string)posted["id"]!, [new { billLineId = billLine, quantity = 8m }]);
        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsync($"{Pur}/returns/{(string)over["id"]!}/post", null)).StatusCode);
    }

    [Fact]
    public async Task A_bill_already_paid_keeps_the_excess_as_a_debit_with_the_vendor()
    {
        var (k, po, line, bill) = await BilledOrderAsync();
        var bank = (string)(await Ok(await k.C.GetAsync($"{Fin}/accounts?pageSize=500"))).AsArray().First(a => (string?)a!["code"] == "1010" || (string?)a!["subcategory"] == "Bank")!["id"]!;
        await Ok(await k.C.PostAsJsonAsync($"{Fin}/payments", new { legalEntityId = k.Entity, partyId = k.Vendor, date = (string)bill["billDate"]!, method = "BankTransfer", bankAccountId = bank, amount = 1000m, allocations = new[] { new { billId = (string)bill["id"]!, amount = 1000m } } }), HttpStatusCode.Created);
        Assert.Equal(0m, await k.BalanceAsync(Ap));

        var posted = await PostAsync(k, await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 2m }]));

        Assert.Equal(0m, (decimal)posted["billShares"]![0]!["appliedToBill"]!);
        Assert.Equal(200m, await k.BalanceAsync(Ap)); // the vendor owes this back
        Assert.Equal(1000m, (decimal)(await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)bill["id"]!}")))["paid"]!);
    }

    [Fact]
    public async Task More_than_on_hand_or_received_cannot_go_back()
    {
        var (k, po, line, _) = await BilledOrderAsync();
        var overReceived = await k.C.PostAsJsonAsync($"{Pur}/returns", new { purchaseOrderId = po, returnDate = "2026-07-01", reason = "Too many", lines = new[] { new { orderLineId = line, quantity = 11m } } });
        Assert.Equal(HttpStatusCode.BadRequest, overReceived.StatusCode);

        // stock has since been sold, so only what is on hand can go back
        var (so, soLine) = await k.ConfirmedSaleAsync(quantity: 9m);
        await Ok(await k.DeliverAsync(so, soLine, 9m));
        var draft = await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 2m }], date: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsync($"{Pur}/returns/{(string)draft["id"]!}/post", null)).StatusCode);
        Assert.Equal(-1000m, await k.BalanceAsync(Ap));
        Assert.Equal(1m, (await k.StockAsync()).Quantity);
    }

    [Fact]
    public async Task Voiding_a_return_restores_stock_payables_the_bill_and_the_order_and_leaves_an_audit_record()
    {
        var (k, po, line, bill) = await BilledOrderAsync();
        var posted = await PostAsync(k, await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 3m }]));
        var id = (string)posted["id"]!;

        var voided = await Ok(await k.C.PostAsJsonAsync($"{Pur}/returns/{id}/void", new { reason = "Vendor refused them" }));

        Assert.Equal("voided", (string)voided["status"]!, ignoreCase: true);
        Assert.Equal((10m, 1000m), await k.StockAsync());
        Assert.Equal(1000m, await k.BalanceAsync(Stock));
        Assert.Equal(-1000m, await k.BalanceAsync(Ap));
        var after = await Ok(await k.C.GetAsync($"{Fin}/bills/{(string)bill["id"]!}"));
        Assert.Equal(0m, (decimal)after["paid"]!);
        Assert.Equal("issued", (string)after["status"]!, ignoreCase: true);
        var order = await k.OrderAsync(po);
        Assert.Equal("billed", (string)order["status"]!, ignoreCase: true);
        Assert.Equal(10m, (decimal)order["lines"]![0]!["receivedQuantity"]!);
        Assert.Equal(10m, (decimal)order["lines"]![0]!["billedQuantity"]!);
        Assert.Equal(0m, (decimal)order["lines"]![0]!["returnedQuantity"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Pur}/returns/{id}/void", new { reason = "Twice over" })).StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Set<AuditEvent>().IgnoreQueryFilters().AnyAsync(a => a.RecordType == "PurchaseReturn" && a.RecordId == id && a.Action == "Voided"));
    }

    [Fact]
    public async Task Returns_need_their_permissions_and_a_draft_can_be_deleted()
    {
        var (k, po, line, _) = await BilledOrderAsync();
        var draft = await DraftAsync(k, po, null, [new { orderLineId = line, quantity = 1m }]);
        var role = (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/roles", new { name = "PurRet " + Guid.NewGuid().ToString("N")[..6], description = "x", permissions = new[] { "purchasing.returns.read" } }), HttpStatusCode.Created))["id"]!;
        var email = "pr" + Guid.NewGuid().ToString("N")[..10] + "@demo.local";
        await Ok(await k.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Reader", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { role } }), HttpStatusCode.Created);
        var (reader, _) = await f.SignInAsync(email, "Temp#Passw0rd1");

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Pur}/returns/{(string)draft["id"]!}")).StatusCode);
        Assert.Contains((await Ok(await reader.GetAsync($"{Pur}/returns?pageSize=50"))).AsArray(), r => (string?)r!["id"] == (string)draft["id"]!);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync($"{Pur}/returns/{(string)draft["id"]!}/post", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{Pur}/returns/{(string)draft["id"]!}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await k.C.GetAsync($"{Pur}/returns/{(string)draft["id"]!}")).StatusCode);
    }
}
