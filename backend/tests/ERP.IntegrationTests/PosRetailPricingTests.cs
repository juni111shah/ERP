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

/// <summary>What the till charges once Retail's price lists and promotions apply, and how money goes back on a return.</summary>
public class PosRetailPricingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string PosApi = "/api/v1/m/pos";
    private const string Retail = "/api/v1/m/retail";

    private static async Task<JsonNode> Json(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Till(HttpClient C, Guid Entity, Guid Register, Guid Session, string Currency);

    private async Task<Till> OpenTillAsync(decimal opening = 1000m)
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        await c0.PostAsync("/api/v1/modules/retail/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;
        var currency = (string)(await Json(await c.GetAsync("/api/v1/legal-entities?pageSize=200"))).AsArray().First(e => (string)e!["id"]! == entity)!["baseCurrency"]!;
        var reg = await Json(await c.PostAsJsonAsync($"{PosApi}/registers", new { legalEntityId = entity, code = "P-" + Guid.NewGuid().ToString("N")[..6], name = "Pricing till" }));
        var session = await Json(await c.PostAsJsonAsync($"{PosApi}/sessions/open", new { legalEntityId = entity, registerId = (string)reg["id"]!, openingCash = opening }));
        return new Till(c, Guid.Parse(entity), Guid.Parse((string)reg["id"]!), Guid.Parse((string)session["id"]!), currency);
    }

    private static async Task<Guid> ItemAsync(Till t, decimal price = 100m, Guid? tax = null, ItemType type = ItemType.Service)
    {
        var res = await t.C.PostAsJsonAsync("/api/v1/items", new ItemInput("PR-" + Guid.NewGuid().ToString("N")[..8], "Priced item", type, "ea", price, price / 2, tax));
        return Guid.Parse((string)(await Json(res))["id"]!);
    }

    private static async Task<Guid> TaxAsync(Till t, decimal rate)
    {
        var res = await t.C.PostAsJsonAsync("/api/v1/finance/tax-codes", new { code = "TX" + Guid.NewGuid().ToString("N")[..6], name = "Tax", kind = "vat", rate, isDefault = false, legalEntityId = t.Entity });
        return Guid.Parse((string)(await Json(res))["id"]!);
    }

    private static Task<HttpResponseMessage> SellAsync(Till t, Guid item, decimal qty, decimal total, PosPaymentMethod method = PosPaymentMethod.Cash, string? promo = null) =>
        t.C.PostAsJsonAsync($"{PosApi}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, null, null,
            [new PosOrderLineInput(item, Quantity: qty)],
            [new PosPaymentInput(method, total, total, 0m, null)], PromoCode: promo));

    private static async Task<JsonNode> PriceListAsync(Till t, Guid item, decimal price, int type = 0, bool isDefault = true, decimal? minQuantity = null, string? currency = null, string? validTo = null, bool isActive = true)
        => await Json(await t.C.PostAsJsonAsync($"{Retail}/pricelists", new
        {
            legalEntityId = t.Entity, code = "PL-" + Guid.NewGuid().ToString("N")[..8], name = "List", type, currency, validTo, isDefault, isActive,
            items = new[] { new { itemId = item, itemCode = "X", itemName = "X", basePrice = 0m, price, minQuantity } },
        }));

    private static async Task<(string Id, string Code)> PromotionAsync(Till t, int type, decimal value, int? usageLimit = null, decimal? minimum = null)
    {
        var code = "SAVE" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var promo = await Json(await t.C.PostAsJsonAsync($"{Retail}/promotions", new
        {
            legalEntityId = t.Entity, code, name = "Promo", type, discountValue = value, minimumOrderAmount = minimum,
            startDate = today.AddDays(-1).ToString("yyyy-MM-dd"), endDate = today.AddDays(5).ToString("yyyy-MM-dd"), usageLimit, isActive = true,
        }));
        return ((string)promo["id"]!, code);
    }

    private static async Task<int> TimesUsedAsync(Till t, string promotionId) => (int)(await Json(await t.C.GetAsync($"{Retail}/promotions/{promotionId}")))["timesUsed"]!;

    private static async Task<Dictionary<string, decimal>> BalancesAsync(Till t)
    {
        var tb = await Json(await t.C.GetAsync($"/api/v1/finance/reports/trial-balance?to=2099-12-31&legalEntityId={t.Entity}"));
        Assert.True((bool)tb["balanced"]!, "trial balance must balance");
        return tb["rows"]!.AsArray().ToDictionary(r => (string)r!["code"]!, r => (decimal)r!["debit"]! - (decimal)r["credit"]!);
    }

    private static decimal Delta(Dictionary<string, decimal> before, Dictionary<string, decimal> after, string code) => after.GetValueOrDefault(code) - before.GetValueOrDefault(code);

    private async Task<HttpClient> SignInWithRoleAsync(Till t, params string[] permissions)
    {
        var role = await Json(await t.C.PostAsJsonAsync("/api/v1/roles", new RoleInput("Role_" + Guid.NewGuid().ToString("N")[..8], "Pricing test role", permissions)));
        var email = $"pricing_{Guid.NewGuid():N}@demo.local";
        (await t.C.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Pricing User", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { Guid.Parse((string)role["id"]!) } })).EnsureSuccessStatusCode();
        var (client, _) = await f.SignInAsync(email, "Temp#Passw0rd");
        return client;
    }

    // ---- price lists ----

    [Fact]
    public async Task The_till_charges_the_active_price_list_and_shows_it_in_the_catalogue()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        await PriceListAsync(t, item, 80m);

        var catalog = await Json(await t.C.GetAsync($"{PosApi}/terminal/catalog?registerId={t.Register}&pageSize=200&search=Priced"));
        Assert.Equal(80m, (decimal)catalog["items"]!.AsArray().First(i => (Guid)i!["id"]! == item)!["price"]!);

        // The old catalogue amount is now an over-payment: the server's price is the list's.
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(t, item, 1, 100m)).StatusCode);

        var order = await Json(await SellAsync(t, item, 1, 80m));
        Assert.Equal(80m, (decimal)order["lines"]![0]!["unitPrice"]!);
        Assert.Equal(80m, (decimal)order["totalAmount"]!);
    }

    [Fact]
    public async Task A_list_that_is_inactive_expired_in_another_currency_or_a_non_default_standard_list_does_not_apply()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        await PriceListAsync(t, item, 10m, isActive: false);
        await PriceListAsync(t, item, 20m, validTo: DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"));
        await PriceListAsync(t, item, 30m, currency: t.Currency == "XXX" ? "YYY" : "XXX");
        await PriceListAsync(t, item, 40m, isDefault: false);
        await PriceListAsync(t, item, 50m, type: 3); // member prices need a customer on the ticket

        var quote = await Json(await t.C.PostAsJsonAsync($"{PosApi}/orders/quote", new { legalEntityId = t.Entity, lines = new[] { new { itemId = item, quantity = 1m } } }));
        Assert.Equal(100m, (decimal)quote["lines"]![0]!["unitPrice"]!);
    }

    [Fact]
    public async Task Quantity_breaks_apply_to_the_whole_order_and_the_quote_matches_the_sale()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 10m);
        var item = await ItemAsync(t, 100m, tax);
        await PriceListAsync(t, item, 70m, type: 2, minQuantity: 5m);

        var one = await Json(await t.C.PostAsJsonAsync($"{PosApi}/orders/quote", new { legalEntityId = t.Entity, lines = new[] { new { itemId = item, quantity = 1m } } }));
        Assert.Equal(110m, (decimal)one["totalAmount"]!);

        var two = new[] { new { itemId = item, quantity = 2m }, new { itemId = item, quantity = 3m } };
        var quote = await Json(await t.C.PostAsJsonAsync($"{PosApi}/orders/quote", new { legalEntityId = t.Entity, lines = two }));
        Assert.Equal(385m, (decimal)quote["totalAmount"]!); // 5 x 70 + 10%
    }

    [Fact]
    public async Task A_seller_without_override_cannot_go_below_the_list_price()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        await PriceListAsync(t, item, 80m);
        var seller = await SignInWithRoleAsync(t, Permissions.OrdersCreate);

        var low = await seller.PostAsJsonAsync($"{PosApi}/orders/quote", new { legalEntityId = t.Entity, lines = new[] { new { itemId = item, quantity = 1m, unitPrice = 70m } } });
        Assert.Equal(HttpStatusCode.BadRequest, low.StatusCode);
    }

    // ---- promotions ----

    [Fact]
    public async Task A_promo_code_discounts_before_tax_counts_its_use_and_a_void_gives_it_back()
    {
        var t = await OpenTillAsync();
        var tax = await TaxAsync(t, 10m);
        var item = await ItemAsync(t, 100m, tax);
        var (promoId, code) = await PromotionAsync(t, type: 0, value: 25m, usageLimit: 1);
        var before = await BalancesAsync(t);

        var quote = await Json(await t.C.PostAsJsonAsync($"{PosApi}/orders/quote", new { legalEntityId = t.Entity, lines = new[] { new { itemId = item, quantity = 2m } }, promoCode = code.ToLowerInvariant() }));
        Assert.Equal((150m, 15m, 165m, 50m), ((decimal)quote["subTotal"]!, (decimal)quote["taxTotal"]!, (decimal)quote["totalAmount"]!, (decimal)quote["promoDiscount"]!));
        Assert.Equal(0, await TimesUsedAsync(t, promoId)); // a quote uses nothing

        var order = await Json(await SellAsync(t, item, 2, 165m, promo: code));
        Assert.Equal(code, (string)order["promoCode"]!);
        Assert.Equal(50m, (decimal)order["promoDiscount"]!);
        Assert.Equal(50m, (decimal)order["discountTotal"]!);
        Assert.Equal(1, await TimesUsedAsync(t, promoId));

        var sold = await BalancesAsync(t);
        Assert.Equal(165m, Delta(before, sold, "1000"));
        Assert.Equal(-150m, Delta(before, sold, "4100"));
        Assert.Equal(-15m, Delta(before, sold, "2100"));

        // The limit of one is spent.
        var another = await SellAsync(t, item, 2, 165m, promo: code);
        Assert.Equal(HttpStatusCode.BadRequest, another.StatusCode);
        Assert.Equal(1, await TimesUsedAsync(t, promoId));

        (await t.C.PostAsJsonAsync($"{PosApi}/orders/{(string)order["id"]!}/void", new { reason = "Rung up by mistake" })).EnsureSuccessStatusCode();
        Assert.Equal(0, await TimesUsedAsync(t, promoId));
        (await SellAsync(t, item, 2, 165m, promo: code)).EnsureSuccessStatusCode();
        Assert.Equal(1, await TimesUsedAsync(t, promoId));
    }

    [Fact]
    public async Task A_fixed_promo_is_shared_across_lines_and_a_return_gives_back_what_was_paid()
    {
        var t = await OpenTillAsync();
        var first = await ItemAsync(t, 30m);
        var second = await ItemAsync(t, 70m);
        var (_, code) = await PromotionAsync(t, type: 1, value: 10m);

        var order = await Json(await t.C.PostAsJsonAsync($"{PosApi}/orders", new CreatePosOrderCommand(t.Entity, t.Register, t.Session, null, null,
            [new PosOrderLineInput(first), new PosOrderLineInput(second)],
            [new PosPaymentInput(PosPaymentMethod.Cash, 90m, 90m, 0m, null)], PromoCode: code)));
        Assert.Equal(90m, (decimal)order["totalAmount"]!);
        var lines = order["lines"]!.AsArray();
        Assert.Equal(27m, (decimal)lines.First(l => (Guid)l!["itemId"]! == first)!["lineTotal"]!);
        Assert.Equal(63m, (decimal)lines.First(l => (Guid)l!["itemId"]! == second)!["lineTotal"]!);

        var line = Guid.Parse((string)lines.First(l => (Guid)l!["itemId"]! == second)!["id"]!);
        var ret = await Json(await t.C.PostAsJsonAsync($"{PosApi}/returns", new CreatePosReturnCommand(Guid.Parse((string)order["id"]!), t.Session, "Back", [new PosReturnLineInput(line, 1)], [new PosRefundInput(PosPaymentMethod.Cash, 63m)])));
        Assert.Equal(63m, (decimal)ret["totalAmount"]!);
    }

    [Fact]
    public async Task A_code_that_is_unknown_under_its_minimum_or_of_a_type_with_no_amount_is_refused()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        var (_, minimum) = await PromotionAsync(t, type: 0, value: 10m, minimum: 500m);
        var (bundleId, bundle) = await PromotionAsync(t, type: 3, value: 10m);

        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(t, item, 1, 100m, promo: "NO-SUCH-CODE")).StatusCode);
        var under = await SellAsync(t, item, 1, 90m, promo: minimum);
        Assert.Equal(HttpStatusCode.BadRequest, under.StatusCode);
        Assert.Contains(t.Currency, await under.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(t, item, 1, 100m, promo: bundle)).StatusCode);
        Assert.Equal(0, await TimesUsedAsync(t, bundleId));
    }

    // ---- currency ----

    [Fact]
    public async Task A_price_list_without_a_currency_takes_the_legal_entitys_and_the_promo_message_names_it()
    {
        var t = await OpenTillAsync();
        var item = await ItemAsync(t, 100m);
        var list = await PriceListAsync(t, item, 95m);
        Assert.Equal(t.Currency, (string)list["currency"]!);

        var explicitList = await PriceListAsync(t, item, 500m, currency: "eur", isDefault: false);
        Assert.Equal("EUR", (string)explicitList["currency"]!);

        var (_, code) = await PromotionAsync(t, type: 0, value: 10m, minimum: 500m);
        var result = await Json(await t.C.PostAsJsonAsync($"{Retail}/promotions/validate", new { legalEntityId = t.Entity, promoCode = code, orderTotal = 10m }));
        Assert.False((bool)result["isValid"]!);
        Assert.Contains(t.Currency, (string)result["message"]!);
        if (t.Currency != "QAR") Assert.DoesNotContain("QAR", (string)result["message"]!);
    }
}
