using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Domain.Platform;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ERP.IntegrationTests.SupplyChainKit;

namespace ERP.IntegrationTests;

/// <summary>Sales returns: a numbered document that restocks at the cost the goods left at, reverses that cost and credits the customer.</summary>
public class SalesReturnTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Stock = "inventory";
    private const string Cogs = "cogs";
    private const string Ar = "ar.control";
    private const string Income = "income.sales";

    /// <summary>10 in stock at 50; 5 sold at 200, delivered and invoiced.</summary>
    private async Task<(SupplyChainKit K, string Order, string Line, string Invoice)> InvoicedSaleAsync(decimal taxRate = 0m)
    {
        var k = await NewAsync(f);
        if (taxRate > 0) await k.NewTaxCodeAsync(taxRate);
        await k.OpeningStockAsync(10m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m, price: 200m, taxRate: taxRate);
        await Ok(await k.DeliverAsync(so, line, 5m));
        var invoice = (string)(await Ok(await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null)))["id"]!;
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{invoice}/issue", null));
        return (k, so, line, invoice);
    }

    private static async Task<JsonNode> DraftAsync(SupplyChainKit k, string? order, string? invoice, object[] lines, string date = "2026-07-01")
        => await Ok(await k.C.PostAsJsonAsync($"{Sales}/returns", new { salesOrderId = order, invoiceId = invoice, returnDate = date, reason = "Damaged in transit", lines }), HttpStatusCode.Created);

    private static async Task<JsonNode> PostAsync(SupplyChainKit k, JsonNode ret) => await Ok(await k.C.PostAsync($"{Sales}/returns/{(string)ret["id"]!}/post", null));

    [Fact]
    public async Task A_return_against_an_invoiced_order_restocks_reverses_cost_and_credits_the_customer()
    {
        var (k, so, line, _) = await InvoicedSaleAsync();
        Assert.Equal((5m, 250m), await k.StockAsync());
        Assert.Equal(250m, await k.BalanceAsync(Cogs));
        Assert.Equal(1000m, await k.BalanceAsync(Ar));

        var draft = await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 2m }]);
        Assert.StartsWith("SR-", (string)draft["number"]!);
        Assert.Equal("draft", (string)draft["status"]!, ignoreCase: true);
        Assert.Equal((5m, 250m), await k.StockAsync()); // a draft does nothing

        var posted = await PostAsync(k, draft);

        Assert.Equal("posted", (string)posted["status"]!, ignoreCase: true);
        Assert.Equal(400m, (decimal)posted["totalAmount"]!);
        var note = await Ok(await k.C.GetAsync($"{Fin}/invoices/{(string)posted["creditNoteId"]!}"));
        Assert.True((bool)note["isCreditNote"]!);
        Assert.Equal("issued", (string)note["status"]!);
        Assert.Equal((7m, 350m), await k.StockAsync());
        Assert.Equal(150m, await k.BalanceAsync(Cogs));
        Assert.Equal(350m, await k.BalanceAsync(Stock));
        Assert.Equal(600m, await k.BalanceAsync(Ar));
        Assert.Equal(-600m, await k.BalanceAsync(Income));
        Assert.Equal(2m, (decimal)(await k.SaleAsync(so))["lines"]![0]!["returnedQuantity"]!);

        // only three are left to come back
        var tooMany = await k.C.PostAsJsonAsync($"{Sales}/returns", new { salesOrderId = so, returnDate = "2026-07-01", reason = "More", lines = new[] { new { orderLineId = line, quantity = 4m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Goods_come_back_at_the_cost_they_left_at_not_the_current_average()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(2m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 4m, price: 200m);
        await Ok(await k.DeliverAsync(so, line, 2m));          // two leave at 50
        await k.OpeningStockAsync(2m, 70m, date: DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        await Ok(await k.DeliverAsync(so, line, 4m));          // two leave at 70
        Assert.Equal(240m, await k.BalanceAsync(Cogs));
        Assert.Equal((0m, 0m), await k.StockAsync());

        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 1m }], today));

        Assert.Equal(170m, await k.BalanceAsync(Cogs)); // the newest issue, at 70
        Assert.Equal((1m, 70m), await k.StockAsync());
        Assert.Equal(70m, await k.BalanceAsync(Stock));

        await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 2m }], today));

        Assert.Equal(50m, await k.BalanceAsync(Cogs)); // the second 70, then a 50: nothing is reversed twice
        Assert.Equal((3m, 190m), await k.StockAsync());
    }

    [Fact]
    public async Task A_return_against_the_invoice_credits_tax_too()
    {
        var (k, so, _, invoice) = await InvoicedSaleAsync(taxRate: 5m);
        var inv = await Ok(await k.C.GetAsync($"{Fin}/invoices/{invoice}"));
        Assert.Equal(1050m, await k.BalanceAsync(Ar));
        Assert.Equal(-50m, await k.BalanceAsync("tax.output"));

        var posted = await PostAsync(k, await DraftAsync(k, null, invoice, [new { invoiceLineId = (string)inv["lines"]![0]!["id"]!, quantity = 1m }]));

        Assert.Equal(210m, (decimal)posted["totalAmount"]!);
        Assert.Equal(so, (string)posted["salesOrderId"]!);
        Assert.Equal(840m, await k.BalanceAsync(Ar));
        Assert.Equal(-40m, await k.BalanceAsync("tax.output"));
        Assert.Equal(200m, await k.BalanceAsync(Cogs));
        Assert.Equal(6m, (await k.StockAsync()).Quantity);
    }

    [Fact]
    public async Task A_return_before_invoicing_reopens_the_order_with_no_credit_note()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);
        await Ok(await k.DeliverAsync(so, line, 5m));
        Assert.Equal("delivered", (string)(await k.SaleAsync(so))["status"]!, ignoreCase: true);

        var posted = await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 5m }]));

        Assert.Null(posted["creditNoteId"]);
        Assert.Equal(0m, await k.BalanceAsync(Ar));
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
        Assert.Equal((10m, 500m), await k.StockAsync());
        var order = await k.SaleAsync(so);
        Assert.Equal("confirmed", (string)order["status"]!, ignoreCase: true);
        Assert.Equal(0m, (decimal)order["lines"]![0]!["deliveredQuantity"]!);
        await Ok(await k.C.PutAsJsonAsync($"{Sales}/orders/{so}/status", new { status = "Cancelled" })); // nothing is out any more
    }

    [Fact]
    public async Task Goods_that_come_back_unusable_credit_the_customer_and_stay_in_cost_of_sales()
    {
        var (k, so, line, _) = await InvoicedSaleAsync();

        await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 2m, restock = false }]));

        Assert.Equal(600m, await k.BalanceAsync(Ar));
        Assert.Equal(250m, await k.BalanceAsync(Cogs));
        Assert.Equal((5m, 250m), await k.StockAsync());
    }

    [Fact]
    public async Task A_manual_invoice_moved_no_stock_so_its_return_only_credits()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var inv = await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = k.Entity, partyId = k.Customer, invoiceDate = "2026-06-01", lines = new[] { new { description = "Item", quantity = 2m, price = 100m, itemId = k.Item } } }), HttpStatusCode.Created);
        await Ok(await k.C.PostAsync($"{Fin}/invoices/{(string)inv["id"]!}/issue", null));

        await PostAsync(k, await DraftAsync(k, null, (string)inv["id"]!, [new { invoiceLineId = (string)inv["lines"]![0]!["id"]!, quantity = 1m }]));

        Assert.Equal(100m, await k.BalanceAsync(Ar));
        Assert.Equal(0m, await k.BalanceAsync(Cogs));
        Assert.Equal((10m, 500m), await k.StockAsync());
    }

    [Fact]
    public async Task Voiding_a_return_reverses_stock_cost_and_credit_and_leaves_an_audit_record()
    {
        var (k, so, line, invoice) = await InvoicedSaleAsync();
        var posted = await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 2m }]));
        var id = (string)posted["id"]!;

        Assert.Equal(HttpStatusCode.BadRequest, (await k.C.PostAsJsonAsync($"{Sales}/returns/{id}/void", new { reason = "" })).StatusCode);
        // the invoice cannot be voided while the return's credit note stands
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Fin}/invoices/{invoice}/void", new { reason = "Wrong" })).StatusCode);

        var voided = await Ok(await k.C.PostAsJsonAsync($"{Sales}/returns/{id}/void", new { reason = "Goods never arrived" }));

        Assert.Equal("voided", (string)voided["status"]!, ignoreCase: true);
        Assert.Equal("Goods never arrived", (string)voided["voidReason"]!);
        Assert.Equal((5m, 250m), await k.StockAsync());
        Assert.Equal(250m, await k.BalanceAsync(Cogs));
        Assert.Equal(1000m, await k.BalanceAsync(Ar));
        Assert.Equal(0m, (decimal)(await k.SaleAsync(so))["lines"]![0]!["returnedQuantity"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Sales}/returns/{id}/void", new { reason = "Twice over" })).StatusCode);

        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Set<AuditEvent>().IgnoreQueryFilters().AnyAsync(a => a.RecordType == "SalesReturn" && a.RecordId == id && a.Action == "Voided"));
    }

    [Fact]
    public async Task A_return_whose_credit_note_was_applied_cannot_be_voided()
    {
        var (k, so, line, invoice) = await InvoicedSaleAsync();
        var posted = await PostAsync(k, await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 2m }]));
        await Ok(await k.C.PostAsJsonAsync($"{Fin}/invoices/{(string)posted["creditNoteId"]!}/apply-credit", new { invoiceId = invoice, amount = 400m }));

        Assert.Equal(HttpStatusCode.Conflict, (await k.C.PostAsJsonAsync($"{Sales}/returns/{(string)posted["id"]!}/void", new { reason = "Changed my mind" })).StatusCode);

        Assert.Equal((7m, 350m), await k.StockAsync());
        Assert.Equal(600m, await k.BalanceAsync(Ar));
    }

    [Fact]
    public async Task A_draft_invoice_blocks_the_return_and_a_draft_return_can_be_deleted()
    {
        var k = await NewAsync(f);
        await k.OpeningStockAsync(10m, 50m);
        var (so, line) = await k.ConfirmedSaleAsync(quantity: 5m);
        await Ok(await k.DeliverAsync(so, line, 5m));
        var draft = await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 1m }]);
        Assert.Equal(HttpStatusCode.NoContent, (await k.C.DeleteAsync($"{Sales}/returns/{(string)draft["id"]!}")).StatusCode);

        await Ok(await k.C.PostAsync($"{Sales}/orders/{so}/convert-to-invoice", null)); // a draft invoice
        var res = await k.C.PostAsJsonAsync($"{Sales}/returns", new { salesOrderId = so, returnDate = "2026-07-01", reason = "Early", lines = new[] { new { orderLineId = line, quantity = 1m } } });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Returns_need_their_permissions()
    {
        var (k, so, line, _) = await InvoicedSaleAsync();
        var draft = await DraftAsync(k, so, null, [new { orderLineId = line, quantity = 1m }]);
        var role = (string)(await Ok(await k.C.PostAsJsonAsync("/api/v1/roles", new { name = "ReturnsRead " + Guid.NewGuid().ToString("N")[..6], description = "x", permissions = new[] { "sales.returns.read" } }), HttpStatusCode.Created))["id"]!;
        var email = "rr" + Guid.NewGuid().ToString("N")[..10] + "@demo.local";
        await Ok(await k.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Reader", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { role } }), HttpStatusCode.Created);
        var (reader, _) = await f.SignInAsync(email, "Temp#Passw0rd1");

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"{Sales}/returns/{(string)draft["id"]!}")).StatusCode);
        Assert.Contains((await Ok(await reader.GetAsync($"{Sales}/returns?pageSize=50"))).AsArray(), r => (string?)r!["id"] == (string)draft["id"]!);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync($"{Sales}/returns/{(string)draft["id"]!}/post", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsJsonAsync($"{Sales}/returns", new { salesOrderId = so, returnDate = "2026-07-01", reason = "No", lines = new[] { new { orderLineId = line, quantity = 1m } } })).StatusCode);
    }
}
