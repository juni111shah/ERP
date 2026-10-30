using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Record history (audit trail per record) and receivables ageing by business unit and source.</summary>
public class RecordHistoryAndAgeingTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task A_records_history_lists_who_changed_what_newest_first_and_masks_sensitive_fields()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var party = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "History Co " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", iban = "AE070331234567890123456" }), HttpStatusCode.Created);
        var id = (string)party["id"]!;
        await Ok(await c.PutAsJsonAsync($"/api/v1/parties/{id}", new { name = (string)party["name"]! + " Ltd", kind = "Company", roles = "Customer", iban = "AE070331234567890999999", phone = "+971500000000" }));

        var history = await Ok(await c.GetAsync($"/api/v1/audit/record?recordType=Party&recordId={id}"));
        var items = history.AsArray();
        Assert.True(items.Count >= 2);
        Assert.Equal("Updated", (string)items[0]!["action"]!); // newest first
        Assert.Equal("Created", (string)items[^1]!["action"]!);
        Assert.False(string.IsNullOrEmpty((string?)items[0]!["userName"]));
        var changes = items[0]!["changes"]!.AsArray();
        var name = changes.Single(x => (string)x!["field"]! == "Name")!;
        Assert.EndsWith(" Ltd", (string)name["new"]!);
        var iban = changes.Single(x => (string)x!["field"]! == "Iban")!;
        Assert.StartsWith("*", (string)iban["old"]!); // masked, never the full value
        Assert.DoesNotContain("AE070331234567890123456", history.ToJsonString());
        Assert.DoesNotContain(changes, x => (string)x!["field"]! is "UpdatedAt" or "RowVersion");

        var raw = JsonNode.Parse(await (await c.GetAsync($"/api/v1/audit/record?recordType=Party&recordId={id}&page=1&pageSize=1")).Content.ReadAsStringAsync())!;
        Assert.Single(raw["data"]!.AsArray());
        Assert.True((int)raw["meta"]!["total"]! >= 2);
    }

    [Fact]
    public async Task History_needs_the_audit_permission_and_valid_arguments()
    {
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/audit/record?recordType=Party&recordId=x")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/audit/record?recordType=Party&recordId=x&pageSize=0")).StatusCode);
        var empty = await Ok(await admin.GetAsync($"/api/v1/audit/record?recordType=Party&recordId={Guid.NewGuid()}"));
        Assert.Empty(empty.AsArray());
    }

    [Fact]
    public async Task Receivables_ageing_can_be_filtered_and_grouped_by_business_unit_and_source()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Age " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var party = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Aged " + Guid.NewGuid().ToString("N")[..6], kind = "Company", roles = "Customer", paymentTermsDays = 30 }), HttpStatusCode.Created))["id"]!;
        var unit = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/business-units", new { legalEntityId = entity, code = "BU" + Guid.NewGuid().ToString("N")[..4], name = "Retail branch" }), HttpStatusCode.Created))["id"]!;

        async Task<string> IssueAsync(decimal price)
        {
            var draft = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = party, invoiceDate = "2026-06-01", lines = new[] { new { description = "Work", quantity = 1m, price } } }), HttpStatusCode.Created);
            return (string)(await Ok(await c.PostAsync($"{Fin}/invoices/{(string)draft["id"]!}/issue", null)))["id"]!;
        }
        var direct = await IssueAsync(100m);
        var tagged = await IssueAsync(300m);
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inv = await db.Invoices.IgnoreQueryFilters().SingleAsync(i => i.Id == Guid.Parse(tagged));
            inv.BusinessUnitId = Guid.Parse(unit);
            inv.SourceType = "module.demo";
            await db.SaveChangesAsync();
        }
        var q = $"asOf=2026-08-01&legalEntityId={entity}";

        var byCustomer = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}"));
        Assert.Equal(400m, (decimal)byCustomer["totals"]!["total"]!);

        var byUnit = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=businessUnit"));
        var unitRows = byUnit["rows"]!.AsArray();
        Assert.Equal(300m, (decimal)unitRows.Single(r => (string)r!["partyName"]! == "Retail branch")!["total"]!);
        Assert.Equal(100m, (decimal)unitRows.Single(r => (string)r!["partyName"]! == "No business unit")!["total"]!);

        var onlyUnit = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&businessUnitId={unit}"));
        Assert.Equal(300m, (decimal)onlyUnit["totals"]!["total"]!);

        var bySource = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=sourceType"));
        Assert.Equal(["Direct invoices", "module.demo"], bySource["rows"]!.AsArray().Select(r => (string)r!["partyName"]!).Order().ToArray());
        var onlySource = await Ok(await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&sourceType=module.demo"));
        Assert.Equal(300m, (decimal)onlySource["totals"]!["total"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Fin}/reports/ar-ageing?{q}&groupBy=nonsense")).StatusCode);
        Assert.NotEqual(direct, tagged);
    }
}
