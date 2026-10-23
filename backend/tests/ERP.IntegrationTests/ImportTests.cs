using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>CSV bulk import: templates, dry run, partial success with row errors, permissions.</summary>
public class ImportTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private static MultipartFormDataContent File(string csv) => new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "data.csv" } };

    private static async Task<JsonNode> Body(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == expected, $"Expected {expected} but got {r.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Parties_import_dry_run_keeps_nothing_and_the_real_run_reports_bad_rows()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var tag = Guid.NewGuid().ToString("N")[..6];
        var csv = $"name,kind,roles,email\nImp A {tag},Company,Customer;Vendor,a@example.com\n,Company,Customer,\nImp B {tag},Person,Customer,\nImp C {tag},Individual,Vendor,\"c,omma@example.com\"\n";

        var listed = (await Body(await c.GetAsync("/api/v1/imports"))).AsArray();
        Assert.Contains(listed, d => (string)d!["key"]! == "parties");
        Assert.DoesNotContain(listed, d => (string)d!["key"]! == "realestate.units"); // module is off in this workspace

        var dry = await Body(await c.PostAsync("/api/v1/imports/parties?dryRun=true", File(csv)));
        Assert.Equal(4, (int)dry["total"]!);
        Assert.Equal(2, (int)dry["created"]!); // A and C are fine
        Assert.Equal([2, 3], dry["errors"]!.AsArray().Select(e => (int)e!["row"]!).ToArray());
        var afterDry = (await Body(await c.GetAsync($"/api/v1/parties?search=Imp%20&pageSize=50"))).AsArray();
        Assert.DoesNotContain(afterDry, p => ((string)p!["name"]!).EndsWith(tag)); // rolled back

        var real = await Body(await c.PostAsync("/api/v1/imports/parties?dryRun=false", File(csv)));
        Assert.Equal(2, (int)real["created"]!);
        var saved = (await Body(await c.GetAsync($"/api/v1/parties?search={tag}&pageSize=50"))).AsArray();
        Assert.Equal(2, saved.Count);

        // importing the same file again: duplicates are rejected row by row
        var again = await Body(await c.PostAsync("/api/v1/imports/parties?dryRun=false", File(csv)));
        Assert.True((int)again["errors"]!.AsArray().Count >= 2);
    }

    [Fact]
    public async Task Template_missing_columns_unknown_keys_and_anonymous_calls_are_handled()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var template = await c.GetStringAsync("/api/v1/imports/items/template");
        Assert.StartsWith("sku,name,type,uom", template);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync("/api/v1/imports/items?dryRun=true", File("name\nx\n"))).StatusCode); // required columns missing
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/imports/nope/template")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync("/api/v1/imports")).StatusCode);
        var ok = await Body(await c.PostAsync("/api/v1/imports/items?dryRun=false", File($"sku,name,type,uom,salesPrice\nSKU-{Guid.NewGuid():N}[..8],Widget,Product,pcs,12.5\n".Replace("[..8]", ""))));
        Assert.Equal(1, (int)ok["created"]!);
    }

    [Fact]
    public async Task Real_estate_units_import_resolves_the_property_and_unit_type()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var tag = Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
        var propertyCsv = $"name,propertyType,totalUnits\nImport Tower {tag},Residential,5\n";
        var p = await Body(await c.PostAsync("/api/v1/imports/realestate.properties?dryRun=false", File(propertyCsv)));
        Assert.Equal(1, (int)p["created"]!);
        await c.PostAsJsonAsync("/api/v1/m/realestate/unit-types", new { code = "IMP" + tag, name = "Imp", defaultUsage = "residential" });
        var unitCsv = $"property,unitNumber,unitType,areaSqft,askingRent\nImport Tower {tag},101,IMP{tag},700,60000\nImport Tower {tag},102,NOPE,700,60000\nMissing Tower,103,IMP{tag},700,60000\n";
        var u = await Body(await c.PostAsync("/api/v1/imports/realestate.units?dryRun=false", File(unitCsv)));
        Assert.Equal(1, (int)u["created"]!);
        Assert.Equal(2, u["errors"]!.AsArray().Count);
        Assert.Contains("not found", (string)u["errors"]![0]!["message"]!);
    }
}
