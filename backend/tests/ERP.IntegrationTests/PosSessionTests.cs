using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Application.Catalog;
using ERP.Domain.Catalog;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Features;
using Xunit;

namespace ERP.IntegrationTests;

/// <summary>POS shifts: atomic drawer and close updates, caller identity, one open session per register (TASK-029, TASK-030).</summary>
public class PosSessionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Pos = "/api/v1/m/pos";

    private static async Task<JsonNode> Json(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.IsSuccessStatusCode, $"{res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static async Task<(HttpClient Admin, string Entity, string Register, string UserId)> RegisterAsync(ApiFactory host)
    {
        var (c0, _) = await host.SignInAsync(ApiFactory.AdminEmail);
        await c0.PostAsync("/api/v1/modules/pos/enable", null);
        var (admin, _) = await host.SignInAsync(ApiFactory.AdminEmail);
        var me = await ApiFactory.Json(await admin.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var reg = await Json(await admin.PostAsJsonAsync($"{Pos}/registers", new { legalEntityId = entity, code = "RS-" + Guid.NewGuid().ToString("N")[..5], name = "Shift register" }));
        return (admin, entity, (string)reg["id"]!, (string)me["user"]!["id"]!);
    }

    private static Task<HttpResponseMessage> OpenAsync(HttpClient c, string entity, string register, decimal opening, object? forged = null)
        => c.PostAsJsonAsync($"{Pos}/sessions/open", forged ?? new { legalEntityId = entity, registerId = register, openingCash = opening });

    private static async Task<Guid> ItemAsync(HttpClient c)
        => Guid.Parse((string)(await Json(await c.PostAsJsonAsync("/api/v1/items", new ItemInput("SS-" + Guid.NewGuid().ToString("N")[..6], "Shift item", ItemType.Product, "ea", 50m, 20m, null))))["id"]!);

    private static Task<HttpResponseMessage> SellAsync(HttpClient c, string entity, string register, string session, Guid item)
        => c.PostAsJsonAsync($"{Pos}/orders", new CreatePosOrderCommand(Guid.Parse(entity), Guid.Parse(register), Guid.Parse(session), null, "Walk-in",
            [new PosOrderLineInput(item, Quantity: 1, UnitPrice: 50m)], [new PosPaymentInput(PosPaymentMethod.Cash, 50m, 50m, 0m, null)]));

    private static async Task<JsonNode> SessionRowAsync(HttpClient c, string session)
        => (await Json(await c.GetAsync($"{Pos}/sessions"))).AsArray().First(s => (string)s!["id"]! == session)!;

    [Fact]
    public async Task Pay_in_after_a_cash_sale_keeps_the_sale_and_attributes_the_movement_to_the_caller()
    {
        var (admin, entity, register, userId) = await RegisterAsync(f);
        var forged = Guid.NewGuid();
        var session = (string)(await Json(await OpenAsync(admin, entity, register, 100m, new { legalEntityId = entity, registerId = register, openingCash = 100m, cashierUserId = forged, cashierName = "Someone else" })))["id"]!;
        var item = await ItemAsync(admin);
        (await SellAsync(admin, entity, register, session, item)).EnsureSuccessStatusCode(); // +50

        var tx = await Json(await admin.PostAsJsonAsync($"{Pos}/sessions/{session}/drawer-transaction", new { type = "PayIn", amount = 25m, reason = "Float top-up", performedByUserId = forged }));
        Assert.Equal(userId, (string)tx["performedByUserId"]!);

        var row = await SessionRowAsync(admin, session);
        Assert.Equal(175m, (decimal)row["expectedCash"]!); // 100 + 50 + 25
        Assert.Equal(userId, (string)row["openedByUserId"]!);
        Assert.NotEqual("Someone else", (string)row["cashierName"]!);
    }

    [Fact]
    public async Task Closing_takes_the_difference_from_all_committed_sales_and_the_caller_identity()
    {
        var (admin, entity, register, userId) = await RegisterAsync(f);
        var session = (string)(await Json(await OpenAsync(admin, entity, register, 100m)))["id"]!;
        var item = await ItemAsync(admin);
        (await SellAsync(admin, entity, register, session, item)).EnsureSuccessStatusCode();
        await Json(await admin.PostAsJsonAsync($"{Pos}/sessions/{session}/drawer-transaction", new { type = "PayOut", amount = 20m, reason = "Supplies" })); // expected 130

        var closed = await Json(await admin.PostAsJsonAsync($"{Pos}/sessions/{session}/close", new { actualCash = 125m, closedByUserId = Guid.NewGuid(), notes = "Short 5" }));
        Assert.Equal((130m, 125m, -5m), ((decimal)closed["expectedCash"]!, (decimal)closed["actualCash"]!, (decimal)closed["cashDifference"]!));
        Assert.Equal(userId, (string)closed["closedByUserId"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await SellAsync(admin, entity, register, session, item)).StatusCode); // order after close
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Pos}/sessions/{session}/close", new { actualCash = 1m })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Pos}/sessions/{session}/drawer-transaction", new { type = "PayIn", amount = 1m, reason = "Late" })).StatusCode);
    }

    [Fact]
    public async Task A_second_open_on_a_register_conflicts_until_the_first_session_is_closed()
    {
        var (admin, entity, register, _) = await RegisterAsync(f);
        var first = await Json(await OpenAsync(admin, entity, register, 10m));
        Assert.Equal(HttpStatusCode.Conflict, (await OpenAsync(admin, entity, register, 10m)).StatusCode);
        await Json(await admin.PostAsJsonAsync($"{Pos}/sessions/{(string)first["id"]!}/close", new { actualCash = 10m }));
        (await OpenAsync(admin, entity, register, 10m)).EnsureSuccessStatusCode();
    }

    [SqlServerFact]
    public async Task Ten_parallel_opens_on_one_register_give_exactly_one_session_on_sql_server()
    {
        using var sql = new SqlServerApiFactory();
        var (admin, entity, register, _) = await RegisterAsync(sql);
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            var res = await OpenAsync(admin, entity, register, 10m);
            return (res.StatusCode, Body: await res.Content.ReadAsStringAsync());
        }));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.True(r.StatusCode == HttpStatusCode.Conflict, $"{r.StatusCode}: {r.Body}"));
    }

    [SqlServerFact]
    public async Task Interleaved_sales_and_drawer_movements_never_lose_an_update_on_sql_server()
    {
        using var sql = new SqlServerApiFactory();
        var (admin, entity, register, _) = await RegisterAsync(sql);
        var session = (string)(await Json(await OpenAsync(admin, entity, register, 100m)))["id"]!;
        var item = await ItemAsync(admin);

        var calls = Enumerable.Range(0, 5).SelectMany(_ => new[]
        {
            SellAsync(admin, entity, register, session, item),
            admin.PostAsJsonAsync($"{Pos}/sessions/{session}/drawer-transaction", new { type = "PayIn", amount = 10m, reason = "Top-up" }),
        });
        var responses = await Task.WhenAll(calls);
        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));

        Assert.Equal(100m + 5 * 50m + 5 * 10m, (decimal)(await SessionRowAsync(admin, session))["expectedCash"]!);
    }
}
