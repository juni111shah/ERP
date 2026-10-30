using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The overview: core cards for everyone, module cards only where the module is enabled and the caller may see the data.</summary>
public class DashboardTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static async Task<JsonNode> Ok(HttpResponseMessage res)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == HttpStatusCode.OK, $"Expected OK but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static JsonNode Kpi(JsonNode d, string key) => d["kpis"]!.AsArray().FirstOrDefault(k => (string?)k!["key"] == key) ?? throw new Xunit.Sdk.XunitException($"No KPI {key} in {d.ToJsonString()}");

    [Fact]
    public async Task A_general_workspace_shows_finance_cards_and_no_real_estate_cards()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Dash " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false })))["id"]!;
        var customer = (string)(await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Dash customer", kind = "Company", roles = "Customer" })))["id"]!;
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var draft = await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/finance/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = today, dueDate = today, lines = new[] { new { description = "Work", quantity = 1m, price = 400m } } }));
        await c.PostAsync($"/api/v1/finance/invoices/{(string)draft["id"]!}/issue", null);

        var d = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}"));
        Assert.Equal(400m, (decimal)Kpi(d, "receivables")["value"]!);
        Assert.Equal(0m, (decimal)Kpi(d, "collected")["value"]!);
        Assert.Equal(0m, (decimal)Kpi(d, "payables")["value"]!);
        Assert.Equal(400m, (decimal)Kpi(d, "net-result")["value"]!);
        Assert.DoesNotContain(d["kpis"]!.AsArray(), k => (string?)k!["key"] == "portfolio");
        var billed = d["charts"]!.AsArray().First(ch => (string?)ch!["key"] == "billed-collected")!;
        Assert.Equal(13, billed["categories"]!.AsArray().Count); // default period: today minus 12 months up to today, by calendar month
        Assert.Equal(400m, (decimal)billed["series"]![0]!["values"]!.AsArray().Last()!);
        var ageing = d["charts"]!.AsArray().First(ch => (string?)ch!["key"] == "ar-ageing")!;
        Assert.Equal(400m, (decimal)ageing["series"]![0]!["values"]![0]!); // due today = current
    }

    [Fact]
    public async Task The_period_defaults_to_the_last_year_and_from_to_set_the_period_figures()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Range " + Guid.NewGuid().ToString("N")[..6], countryCode = "QA", baseCurrency = "QAR", isTaxRegistered = false })))["id"]!;
        var customer = (string)(await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Range customer", kind = "Company", roles = "Customer" })))["id"]!;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var iso = (DateOnly d) => d.ToString("yyyy-MM-dd");
        var draft = await ApiFactory.Json(await c.PostAsJsonAsync("/api/v1/finance/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = iso(today), dueDate = iso(today), lines = new[] { new { description = "Work", quantity = 1m, price = 250m } } }));
        await c.PostAsync($"/api/v1/finance/invoices/{(string)draft["id"]!}/issue", null);
        JsonNode Chart(JsonNode d) => d["charts"]!.AsArray().First(ch => (string?)ch!["key"] == "billed-collected")!;

        var d = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}"));
        Assert.Equal(iso(today.AddYears(-1)), (string)d["from"]!);
        Assert.Equal(iso(today), (string)d["to"]!);
        Assert.Equal(250m, (decimal)Kpi(d, "net-result")["value"]!);

        // a past period: period figures are empty, open items (receivables) are still today's
        var past = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}&from={iso(today.AddYears(-3))}&to={iso(today.AddYears(-2))}"));
        Assert.Equal(iso(today.AddYears(-3)), (string)past["from"]!);
        Assert.Equal(0m, (decimal)Kpi(past, "net-result")["value"]!);
        Assert.Equal(250m, (decimal)Kpi(past, "receivables")["value"]!);
        Assert.Equal(13, Chart(past)["categories"]!.AsArray().Count);
        Assert.All(Chart(past)["series"]![0]!["values"]!.AsArray(), v => Assert.Equal(0m, (decimal)v!));

        // a one-month period: two calendar months, the invoice in the last one
        var month = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}&from={iso(today.AddMonths(-1))}&to={iso(today)}"));
        Assert.Equal(2, Chart(month)["categories"]!.AsArray().Count);
        Assert.Equal(250m, (decimal)Chart(month)["series"]![0]!["values"]!.AsArray().Last()!);
        Assert.Equal(250m, (decimal)Kpi(month, "net-result")["value"]!);

        // only "to": the period is the year before it
        var onlyTo = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={entity}&to={iso(today.AddMonths(-6))}"));
        Assert.Equal(iso(today.AddMonths(-6).AddYears(-1)), (string)onlyTo["from"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/dashboard?from={iso(today)}&to={iso(today.AddDays(-1))}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/v1/dashboard?from={iso(today.AddYears(-11))}&to={iso(today)}")).StatusCode);
    }

    [Fact]
    public async Task A_real_estate_workspace_adds_portfolio_cards_and_a_viewer_only_sees_what_they_may_read()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var d = await Ok(await c.GetAsync("/api/v1/dashboard"));
        Assert.NotNull(Kpi(d, "portfolio"));
        Assert.NotNull(Kpi(d, "occupancy"));
        Assert.NotNull(Kpi(d, "contracted-rent"));
        Assert.NotNull(Kpi(d, "work-orders"));
        Assert.Contains(d["charts"]!.AsArray(), ch => (string?)ch!["key"] == "occupancy");

        var onlyEntity = (string)(await ApiFactory.Json(await c.GetAsync("/api/v1/me")))["legalEntities"]![0]!["id"]!;
        var scoped = await Ok(await c.GetAsync($"/api/v1/dashboard?legalEntityId={onlyEntity}"));
        Assert.True((decimal)Kpi(scoped, "portfolio")["value"]! <= (decimal)Kpi(d, "portfolio")["value"]!);

        // a user without permissions gets only the cards they may read
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var roles = await ApiFactory.Json(await admin.GetAsync("/api/v1/roles"));
        var email = $"dv{Guid.NewGuid():N}"[..10] + "@demo.local";
        var role = (string)roles.AsArray().First(r => (string?)r!["name"] == "Viewer")!["id"]!;
        (await admin.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Viewer", temporaryPassword = "Temp#Passw0rd", roleIds = new[] { role } })).EnsureSuccessStatusCode();
        var viewer = (await f.SignInAsync(email, "Temp#Passw0rd")).Client;
        var vd = await Ok(await viewer.GetAsync("/api/v1/dashboard"));
        Assert.Contains(vd["kpis"]!.AsArray(), k => (string?)k!["key"] == "receivables");
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/dashboard")).StatusCode);
    }
}
