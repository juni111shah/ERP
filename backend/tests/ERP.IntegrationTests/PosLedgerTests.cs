using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Domain.Catalog;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>TASK-007: a POS sale reaches the ledger (cash/card, revenue, output tax) and a void reverses it exactly.</summary>
public class PosLedgerTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Json(HttpResponseMessage res) => ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    private sealed record Ctx(HttpClient C, Guid Entity, Guid Register, Guid Session, Guid Item);

    private async Task<Ctx> SetupAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;

        var reg = await c.PostAsJsonAsync("/api/v1/m/pos/registers", new { legalEntityId = entity, code = "REG-" + Guid.NewGuid().ToString("N")[..4], name = "Ledger Register" });
        reg.EnsureSuccessStatusCode();
        var regId = (string)(await Json(reg))["id"]!;
        var open = await c.PostAsJsonAsync("/api/v1/m/pos/sessions/open", new { legalEntityId = entity, registerId = regId, cashierUserId = Guid.NewGuid(), cashierName = "Cashier", openingCash = 0m });
        open.EnsureSuccessStatusCode();
        var sessionId = (string)(await Json(open))["id"]!;

        var tax = await c.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "PT" + Guid.NewGuid().ToString("N")[..6], name = "VAT 5%", kind = "vat", rate = 5, isDefault = false, legalEntityId = entity });
        tax.EnsureSuccessStatusCode();
        var taxId = Guid.Parse((string)(await Json(tax))["id"]!);
        var item = await c.PostAsJsonAsync("/api/v1/items", new ItemInput("PI-" + Guid.NewGuid().ToString("N")[..6], "Ledger item", ItemType.Product, "ea", 100m, 40m, taxId));
        item.EnsureSuccessStatusCode();
        return new Ctx(c, Guid.Parse(entity), Guid.Parse(regId), Guid.Parse(sessionId), Guid.Parse((string)(await Json(item))["id"]!));
    }

    /// <summary>Net debit (debit minus credit) per account code over all time for the legal entity.</summary>
    private static async Task<Dictionary<string, decimal>> BalancesAsync(Ctx x)
    {
        var tb = await Json(await x.C.GetAsync($"/api/v1/finance/reports/trial-balance?to=2099-12-31&legalEntityId={x.Entity}"));
        Assert.True((bool)tb["balanced"]!, "trial balance must balance");
        return tb["rows"]!.AsArray().ToDictionary(r => (string)r!["code"]!, r => (decimal)r!["debit"]! - (decimal)r["credit"]!);
    }

    private static decimal Delta(Dictionary<string, decimal> before, Dictionary<string, decimal> after, string code)
        => after.GetValueOrDefault(code) - before.GetValueOrDefault(code);

    private static Task<HttpResponseMessage> SellAsync(Ctx x, params PosPaymentInput[] payments) =>
        x.C.PostAsJsonAsync("/api/v1/m/pos/orders", new CreatePosOrderCommand(x.Entity, x.Register, x.Session, null, "Walk-in",
            [new PosOrderLineInput(x.Item, Quantity: 2, UnitPrice: 100m, DiscountPercent: 0)], payments.ToList()));

    [Fact]
    public async Task A_cash_sale_posts_cash_revenue_and_output_tax_and_the_trial_balance_balances()
    {
        var x = await SetupAsync();
        var before = await BalancesAsync(x);
        var res = await SellAsync(x, new PosPaymentInput(PosPaymentMethod.Cash, 210m, 250m, 40m, null));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var after = await BalancesAsync(x);

        Assert.Equal(210m, Delta(before, after, "1000"));   // cash on hand
        Assert.Equal(-200m, Delta(before, after, "4000"));  // sales revenue
        Assert.Equal(-10m, Delta(before, after, "2100"));   // output tax payable
    }

    [Fact]
    public async Task A_mixed_cash_and_card_sale_posts_each_tender_to_its_own_account()
    {
        var x = await SetupAsync();
        var before = await BalancesAsync(x);
        var res = await SellAsync(x, new PosPaymentInput(PosPaymentMethod.Cash, 100m, 100m, 0m, null), new PosPaymentInput(PosPaymentMethod.Card, 110m, 110m, 0m, "AUTH1"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var after = await BalancesAsync(x);

        Assert.Equal(100m, Delta(before, after, "1000"));
        Assert.Equal(110m, Delta(before, after, "1010"));
        Assert.Equal(-200m, Delta(before, after, "4000"));
        Assert.Equal(-10m, Delta(before, after, "2100"));
    }

    [Fact]
    public async Task Voiding_the_order_reverses_cash_revenue_and_tax_exactly()
    {
        var x = await SetupAsync();
        var before = await BalancesAsync(x);
        var created = await SellAsync(x, new PosPaymentInput(PosPaymentMethod.Cash, 210m, 210m, 0m, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var orderId = (string)(await Json(created))["id"]!;

        var voided = await x.C.PostAsJsonAsync($"/api/v1/m/pos/orders/{orderId}/void", new { reason = "Customer changed mind" });
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        var after = await BalancesAsync(x);

        Assert.Equal(0m, Delta(before, after, "1000"));
        Assert.Equal(0m, Delta(before, after, "4000"));
        Assert.Equal(0m, Delta(before, after, "2100"));
    }

    [Fact]
    public async Task The_journal_is_sourced_from_the_pos_order()
    {
        var x = await SetupAsync();
        var created = await SellAsync(x, new PosPaymentInput(PosPaymentMethod.Cash, 210m, 210m, 0m, null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var number = (string)(await Json(created))["number"]!;

        var list = await Json(await x.C.GetAsync($"/api/v1/finance/journals?search={number}&pageSize=50"));
        var hit = (list is JsonArray a ? a : list["items"]!.AsArray()).FirstOrDefault(j => ((string?)j!["memo"] ?? "").Contains(number));
        Assert.NotNull(hit);
        Assert.Equal("pos", (string)hit!["sourceType"]!);
    }
}
