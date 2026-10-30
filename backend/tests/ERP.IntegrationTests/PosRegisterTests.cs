using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Domain.Catalog;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>TASK-031 and TASK-033: registers drive warehouse and cash account, inactive or mismatched registers cannot trade, KPIs respect the legal entity.</summary>
public class PosRegisterTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pos = "/api/v1/m/pos";
    private const string Inv = "/api/v1/m/inventory";

    private static async Task<JsonNode> Json(HttpResponseMessage res) => ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<HttpClient> ClientAsync()
    {
        var (c0, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        await c0.PostAsync("/api/v1/modules/inventory/enable", null);
        return (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
    }

    private static async Task<string> NewEntityAsync(HttpClient c)
        => (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "PosR " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> NewRegisterAsync(HttpClient c, string entity, string? warehouse = null, string? cashAccount = null)
        => (string)(await Ok(await c.PostAsJsonAsync($"{Pos}/registers", new { legalEntityId = entity, code = "R" + Guid.NewGuid().ToString("N")[..6], name = "Till", warehouseId = warehouse, defaultCashAccountId = cashAccount, isActive = true }), HttpStatusCode.Created))["id"]!;

    private static Task<HttpResponseMessage> OpenAsync(HttpClient c, string entity, string register)
        => c.PostAsJsonAsync($"{Pos}/sessions/open", new { legalEntityId = entity, registerId = register, cashierUserId = Guid.NewGuid(), cashierName = "Cashier", openingCash = 0m });

    private static async Task<string> NewSessionAsync(HttpClient c, string entity, string register)
        => (string)(await Ok(await OpenAsync(c, entity, register), HttpStatusCode.Created))["id"]!;

    private static async Task<Guid> NewItemAsync(HttpClient c)
        => Guid.Parse((string)(await Ok(await c.PostAsJsonAsync("/api/v1/items", new ItemInput("RG-" + Guid.NewGuid().ToString("N")[..6], "Register item", ItemType.Product, "ea", 100m, 40m, null)), HttpStatusCode.Created))["id"]!);

    private static Task<HttpResponseMessage> SellAsync(HttpClient c, string entity, string register, string session, Guid item, int qty = 2)
        => c.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(Guid.Parse(entity), Guid.Parse(register), Guid.Parse(session), null, "Walk-in",
            [new PosOrderLineInput(item, Quantity: qty, UnitPrice: 100m, DiscountPercent: 0)], [new PosPaymentInput(PosPaymentMethod.Cash, 100m * qty, 100m * qty, 0m, null)]));

    private static async Task<Dictionary<string, decimal>> BalancesAsync(HttpClient c, string entity)
    {
        var tb = await Json(await c.GetAsync($"/api/v1/finance/reports/trial-balance?to=2099-12-31&legalEntityId={entity}"));
        return tb["rows"]!.AsArray().ToDictionary(r => (string)r!["code"]!, r => (decimal)r!["debit"]! - (decimal)r["credit"]!);
    }

    [Fact]
    public async Task A_sale_issues_stock_from_the_register_warehouse()
    {
        var c = await ClientAsync();
        var entity = await NewEntityAsync(c);
        var wh = (string)(await Ok(await c.PostAsJsonAsync($"{Inv}/warehouses", new { code = "W" + Guid.NewGuid().ToString("N")[..6], name = "Shop floor", legalEntityId = entity }), HttpStatusCode.Created))["id"]!;
        var item = await NewItemAsync(c);
        var accounts = (await Ok(await c.GetAsync("/api/v1/finance/accounts?pageSize=300"), HttpStatusCode.OK)).AsArray();
        var offset = (string)accounts.First(a => (string?)a!["code"] == "2000")!["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Inv}/stock/receive", new { warehouseId = wh, itemId = item, quantity = 10, unitCost = 40m, date = "2026-05-01", offsetAccountId = offset }), HttpStatusCode.Created);

        var register = await NewRegisterAsync(c, entity, warehouse: wh);
        var session = await NewSessionAsync(c, entity, register);
        var res = await SellAsync(c, entity, register, session, item);
        var order = await Ok(res, HttpStatusCode.Created);

        var moves = (await Ok(await c.GetAsync($"{Inv}/movements?pageSize=100&filter=reference:eq:{(string)order["number"]!}"), HttpStatusCode.OK)).AsArray();
        Assert.NotEmpty(moves);
        Assert.All(moves, m => Assert.Equal(wh, (string)m!["warehouseId"]!));
    }

    [Fact]
    public async Task The_cash_debit_goes_to_the_register_default_cash_account_when_set()
    {
        var c = await ClientAsync();
        var entity = await NewEntityAsync(c);
        var accounts = (await Ok(await c.GetAsync("/api/v1/finance/accounts?pageSize=300"), HttpStatusCode.OK)).AsArray();
        var till = (string)accounts.First(a => (string?)a!["code"] == "6900")!["id"]!;
        var item = await NewItemAsync(c);

        var plain = await NewRegisterAsync(c, entity);
        var special = await NewRegisterAsync(c, entity, cashAccount: till);
        var sPlain = await NewSessionAsync(c, entity, plain);
        var sSpecial = await NewSessionAsync(c, entity, special);

        var before = await BalancesAsync(c, entity);
        await Ok(await SellAsync(c, entity, plain, sPlain, item), HttpStatusCode.Created);
        var mid = await BalancesAsync(c, entity);
        Assert.Equal(200m, mid.GetValueOrDefault("1000") - before.GetValueOrDefault("1000"));
        Assert.Equal(0m, mid.GetValueOrDefault("6900") - before.GetValueOrDefault("6900"));

        await Ok(await SellAsync(c, entity, special, sSpecial, item), HttpStatusCode.Created);
        var after = await BalancesAsync(c, entity);
        Assert.Equal(200m, after.GetValueOrDefault("6900") - mid.GetValueOrDefault("6900"));
        Assert.Equal(0m, after.GetValueOrDefault("1000") - mid.GetValueOrDefault("1000"));
    }

    [Fact]
    public async Task An_inactive_register_cannot_open_a_session_or_take_an_order()
    {
        var c = await ClientAsync();
        var entity = await NewEntityAsync(c);
        var item = await NewItemAsync(c);
        var register = await NewRegisterAsync(c, entity);
        var session = await NewSessionAsync(c, entity, register);
        // an open session blocks deactivation (criterion below), so close it first, then deactivate
        await Ok(await c.PostAsJsonAsync($"{Pos}/sessions/{session}/close", new { sessionId = session, actualCash = 0m, closedByUserId = Guid.NewGuid() }), HttpStatusCode.OK);
        await Ok(await c.PutAsJsonAsync($"{Pos}/registers/{register}", new { id = register, name = "Till", isActive = false }), HttpStatusCode.OK);

        Assert.Equal(HttpStatusCode.Conflict, (await OpenAsync(c, entity, register)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SellAsync(c, entity, register, session, item)).StatusCode);
    }

    [Fact]
    public async Task A_register_with_an_open_session_cannot_be_deactivated_or_deleted()
    {
        var c = await ClientAsync();
        var entity = await NewEntityAsync(c);
        var register = await NewRegisterAsync(c, entity);
        await NewSessionAsync(c, entity, register);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Pos}/registers/{register}", new { id = register, name = "Till", isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"{Pos}/registers/{register}")).StatusCode);
    }

    [Fact]
    public async Task An_order_must_match_the_register_and_legal_entity_of_its_session()
    {
        var c = await ClientAsync();
        var entity = await NewEntityAsync(c);
        var other = await NewEntityAsync(c);
        var item = await NewItemAsync(c);
        var register = await NewRegisterAsync(c, entity);
        var otherRegister = await NewRegisterAsync(c, entity);
        var session = await NewSessionAsync(c, entity, register);

        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(c, entity, otherRegister, session, item)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(c, other, register, session, item)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await OpenAsync(c, other, otherRegister)).StatusCode); // session entity differs from its register's
    }

    [Fact]
    public async Task Kpis_exclude_other_legal_entities_and_voided_orders()
    {
        var c = await ClientAsync();
        var a = await NewEntityAsync(c);
        var b = await NewEntityAsync(c);
        var item = await NewItemAsync(c);
        var regA = await NewRegisterAsync(c, a);
        var regB = await NewRegisterAsync(c, b);
        var sesA = await NewSessionAsync(c, a, regA);
        var sesB = await NewSessionAsync(c, b, regB);

        await Ok(await SellAsync(c, a, regA, sesA, item, qty: 2), HttpStatusCode.Created); // 200
        await Ok(await SellAsync(c, b, regB, sesB, item, qty: 1), HttpStatusCode.Created); // 100, other entity
        var voided = await Ok(await SellAsync(c, a, regA, sesA, item, qty: 3), HttpStatusCode.Created); // 300, voided below
        await Ok(await c.PostAsJsonAsync($"{Pos}/orders/{(string)voided["id"]!}/void", new { reason = "Customer changed their mind" }), HttpStatusCode.OK);

        var kpi = await Ok(await c.GetAsync($"{Pos}/kpis?legalEntityId={a}"), HttpStatusCode.OK);
        Assert.Equal(200m, (decimal)kpi["todaySales"]!);
        Assert.Equal(1, (int)kpi["totalOrdersToday"]!);
        Assert.Equal(200m, (decimal)kpi["averageTicket"]!);
        Assert.Equal(1, (int)kpi["activeRegistersCount"]!);
        Assert.Equal(1, (int)kpi["openSessionsCount"]!);

        var onlyB = await Ok(await c.GetAsync($"{Pos}/kpis?legalEntityId={b}"), HttpStatusCode.OK);
        Assert.Equal(100m, (decimal)onlyB["todaySales"]!);
    }

    [Fact]
    public void The_pos_module_has_no_hardcoded_currency_literal()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "modules", "ERP.Modules.Pos"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var offenders = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "modules", "ERP.Modules.Pos"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(p => File.ReadAllText(p).Contains("\"AED\"") || File.ReadAllText(p).Contains("AED {"))
            .ToList();
        Assert.Empty(offenders);
    }
}
