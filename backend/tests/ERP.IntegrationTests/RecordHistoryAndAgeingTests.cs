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
    public async Task The_audit_log_lists_named_records_without_field_values_and_masks_the_detail()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var iban = "AE070331234567890123456";
        var name = "Audit Log Co " + Guid.NewGuid().ToString("N")[..6];
        var party = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name, kind = "Company", roles = "Customer", iban }), HttpStatusCode.Created);
        var id = (string)party["id"]!;
        await Ok(await c.PutAsJsonAsync($"/api/v1/parties/{id}", new { name = name + " Ltd", kind = "Company", roles = "Customer", iban = "AE070331234567890999999" }));

        var listBody = await (await c.GetAsync($"/api/v1/audit?filter=recordType:eq:Party&filter=recordId:eq:{id}&sort=-occurredAt")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(iban, listBody);
        var rows = ApiFactory.Unwrap(JsonNode.Parse(listBody)!).AsArray();
        var updated = rows.Single(r => (string)r!["action"]! == "Updated")!;
        Assert.Equal(name + " Ltd", (string)updated["recordLabel"]!);
        Assert.Contains("Iban", updated["changedFields"]!.AsArray().Select(x => (string)x!));
        Assert.DoesNotContain("UpdatedAt", updated["changedFields"]!.AsArray().Select(x => (string)x!));
        Assert.Null(updated["before"]);
        Assert.False(string.IsNullOrEmpty((string?)updated["userName"]));

        var detailBody = await (await c.GetAsync($"/api/v1/audit/{updated["id"]}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(iban, detailBody);
        var detail = ApiFactory.Unwrap(JsonNode.Parse(detailBody)!);
        var fields = detail["fields"]!.AsArray();
        var changedName = fields.Single(x => (string)x!["field"]! == "Name")!;
        Assert.True((bool)changedName["changed"]!);
        Assert.Equal(name, (string)changedName["old"]!);
        Assert.StartsWith("*", (string)fields.Single(x => (string)x!["field"]! == "Iban")!["old"]!);
        Assert.Contains(fields, x => !(bool)x!["changed"]!); // the unchanged fields come too, for "show unchanged"
        Assert.DoesNotContain(fields, x => (string)x!["field"]! is "Id" or "UpdatedAt" or "RowVersion");
        Assert.Equal("Company", (string)fields.Single(x => (string)x!["field"]! == "Kind")!["new"]!); // enums read as words
        Assert.Equal(2, (int)detail["recordEventCount"]!);
        Assert.Equal("Updated", (string)detail["event"]!["action"]!);

        // The screen's time ranges and field search use the generic list parameters.
        var since = DateTime.UtcNow.AddHours(-1).ToString("O");
        var inRange = await Ok(await c.GetAsync($"/api/v1/audit?filter=occurredAt:gte:{since}&filter=action:eq:Updated&filter=recordId:eq:{id}"));
        Assert.Single(inRange.AsArray());
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
        var beforeTomorrow = await Ok(await c.GetAsync($"/api/v1/audit?filter=occurredAt:lt:{tomorrow}&filter=recordId:eq:{id}"));
        Assert.Equal(2, beforeTomorrow.AsArray().Count);
        var longAgo = await Ok(await c.GetAsync($"/api/v1/audit?filter=occurredAt:lt:2000-01-01&filter=recordId:eq:{id}"));
        Assert.Empty(longAgo.AsArray());
        var searched = await Ok(await c.GetAsync($"/api/v1/audit?search=Iban&filter=recordId:eq:{id}"));
        Assert.Single(searched.AsArray());

        var facets = await Ok(await c.GetAsync("/api/v1/audit/facets"));
        Assert.Contains("Party", facets["recordTypes"]!.AsArray().Select(x => (string)x!));
        Assert.Contains("Updated", facets["actions"]!.AsArray().Select(x => (string)x!));
        Assert.NotEmpty(facets["users"]!.AsArray());
        Assert.True((int)facets["last7Days"]! >= 2);
    }

    [Fact]
    public async Task The_audit_log_needs_the_audit_permission()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/audit/facets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"/api/v1/audit/{Guid.NewGuid()}")).StatusCode);
        var (admin, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/audit/{Guid.NewGuid()}")).StatusCode);
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
