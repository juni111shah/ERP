using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The salary file is picked by the employing entity's country: UAE format, Qatar stub, generic CSV, or a clear refusal.</summary>
public class WpsBuilderTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<(HttpClient C, string RunId)> PostedRunAsync(string country, string currency)
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Wps " + Guid.NewGuid().ToString("N")[..6], countryCode = country, baseCurrency = currency, isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Hr}/employees", new { legalEntityId = entity, fullName = "Amir Khan", designation = "Agent", department = "Leasing", joinDate = "2020-01-01", status = "active", basicSalary = 6000m, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m, iban = "AE070331234567890123456", labourCardNumber = "12345678901234", bankRoutingCode = "123456789" }), HttpStatusCode.Created);
        var run = await Ok(await c.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        await Ok(await c.PostAsync($"{Hr}/payroll/{id}/post", null));
        return (c, id);
    }

    private static async Task SetAsync(HttpClient c, Dictionary<string, object> values)
        => Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/v1/settings", values)).StatusCode);

    [Fact]
    public async Task A_country_without_a_format_gets_a_clear_409()
    {
        var (c, id) = await PostedRunAsync("OM", "OMR");
        await SetAsync(c, new() { ["hr.wps.columns"] = "" });
        var res = await c.GetAsync($"{Hr}/payroll/{id}/wps-sif");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("No salary-file format is set up for OM.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Qatar_is_refused_until_the_bank_template_is_provided()
    {
        var (c, id) = await PostedRunAsync("QA", "QAR");
        var res = await c.GetAsync($"{Hr}/payroll/{id}/wps-sif");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("The Qatar salary file layout has not been configured. Provide the bank template.", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_configurable_csv_is_used_when_the_country_has_no_builder_and_the_columns_are_set()
    {
        var (c, id) = await PostedRunAsync("OM", "OMR");
        await SetAsync(c, new() { ["hr.wps.columns"] = "employeeNumber,name,iban,netPay,currency", ["hr.wps.header"] = "No,Name,IBAN,Net,Cur" });
        try
        {
            var res = await c.GetAsync($"{Hr}/payroll/{id}/wps-sif");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            var lines = (await res.Content.ReadAsStringAsync()).TrimEnd('\n').Split('\n');
            Assert.Equal(2, lines.Length);
            Assert.Equal("No,Name,IBAN,Net,Cur", lines[0]);
            Assert.EndsWith(",Amir Khan,AE070331234567890123456,6000.00,OMR", lines[1]);
        }
        finally { await SetAsync(c, new() { ["hr.wps.columns"] = "", ["hr.wps.header"] = "" }); }
    }
}
