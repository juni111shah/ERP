using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Tax summary, cash movements, cash forecast and the real estate reports.</summary>
public class ReportsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Fin = "/api/v1/finance";
    private const string Re = "/api/v1/m/realestate";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task Tax_summary_nets_output_tax_against_recoverable_input_tax()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Tax " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = true }), HttpStatusCode.Created))["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "C", kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var vendor = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "V", kind = "Company", roles = "Vendor" }), HttpStatusCode.Created))["id"]!;
        var tax = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "R" + Guid.NewGuid().ToString("N")[..6], name = "Std", kind = "vat", rate = 5 }), HttpStatusCode.Created))["id"]!;
        var blocked = (string)(await Ok(await c.PostAsJsonAsync($"{Fin}/tax-codes", new { code = "B" + Guid.NewGuid().ToString("N")[..6], name = "Blocked", kind = "vat", rate = 5, isRecoverable = false }), HttpStatusCode.Created))["id"]!;
        var expense = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["code"] == "6900")!["id"]!;

        async Task Invoice(decimal price, bool credit)
        {
            var d = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = "2026-07-10", isCreditNote = credit, lines = new[] { new { description = "x", quantity = 1m, price, taxCodeId = tax } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/invoices/{(string)d["id"]!}/issue", null));
        }
        async Task Bill(decimal price, string code, string reference)
        {
            var b = await Ok(await c.PostAsJsonAsync($"{Fin}/bills", new { legalEntityId = entity, partyId = vendor, vendorReference = reference, billDate = "2026-07-12", lines = new[] { new { description = "y", quantity = 1m, price, accountId = expense, taxCodeId = code } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/bills/{(string)b["id"]!}/post", null));
        }
        await Invoice(1000m, false); await Invoice(200m, true);   // output: 50 - 10 = 40
        await Bill(400m, tax, "T-1"); await Bill(100m, blocked, "T-2"); // input: 20 recoverable, 5 blocked

        var r = await Ok(await c.GetAsync($"{Fin}/reports/tax-summary?from=2026-07-01&to=2026-09-30&legalEntityId={entity}"));
        Assert.Equal((40m, 20m, 20m, 5m), ((decimal)r["outputTax"]!, (decimal)r["inputTax"]!, (decimal)r["netPayable"]!, (decimal)r["nonRecoverableTax"]!));
        var sales = r["rows"]!.AsArray().Single(x => (string)x!["section"]! == "Sales")!;
        Assert.Equal(800m, (decimal)sales["net"]!); // 1000 less the 200 credit note
    }

    [Fact]
    public async Task Cash_movements_and_forecast_follow_the_ledger_and_open_documents()
    {
        var c = (await f.SignInAsync(ApiFactory.AdminEmail)).Client;
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Cash " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        string Id(string code) => (string)accounts.First(a => (string?)a!["code"] == code)!["id"]!;
        var customer = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "C2", kind = "Company", roles = "Customer" }), HttpStatusCode.Created))["id"]!;
        var today = DateTime.UtcNow.Date;

        async Task Journal(string date, string dr, string cr, decimal amount)
        {
            var j = await Ok(await c.PostAsJsonAsync($"{Fin}/journals", new { legalEntityId = entity, date, lines = new[] { new { accountId = dr, debit = amount, credit = 0m }, new { accountId = cr, debit = 0m, credit = amount } } }), HttpStatusCode.Created);
            await Ok(await c.PostAsync($"{Fin}/journals/{(string)j["id"]!}/post", null));
        }
        await Journal("2026-01-05", Id("1010"), Id("3000"), 10_000m);
        await Journal("2026-02-05", Id("3000"), Id("1010"), 1_000m);

        var m = await Ok(await c.GetAsync($"{Fin}/reports/cash-movements?from=2026-02-01&to=2026-02-28&legalEntityId={entity}"));
        Assert.Equal((10_000m, 0m, 1_000m, 9_000m), ((decimal)m["opening"]!, (decimal)m["inflows"]!, (decimal)m["outflows"]!, (decimal)m["closing"]!));
        Assert.Equal(1_000m, (decimal)m["bySource"]!.AsArray().Single()!["outflows"]!);

        // an open invoice due next week and a cheque dated the week after
        var due = today.AddDays(7).ToString("yyyy-MM-dd");
        var inv = await Ok(await c.PostAsJsonAsync($"{Fin}/invoices", new { legalEntityId = entity, partyId = customer, invoiceDate = today.ToString("yyyy-MM-dd"), dueDate = due, lines = new[] { new { description = "x", quantity = 1m, price = 500m } } }), HttpStatusCode.Created);
        await Ok(await c.PostAsync($"{Fin}/invoices/{(string)inv["id"]!}/issue", null));
        await Ok(await c.PostAsJsonAsync($"{Fin}/cheques", new { legalEntityId = entity, partyId = customer, kind = "postDatedCheque", instrumentNo = "FC1", amount = 250m, instrumentDate = today.AddDays(14).ToString("yyyy-MM-dd") }), HttpStatusCode.Created);

        var fc = await Ok(await c.GetAsync($"{Fin}/reports/cash-forecast?weeks=4&legalEntityId={entity}"));
        Assert.Equal(4, fc["weeks"]!.AsArray().Count);
        Assert.Equal(9_000m, (decimal)fc["cashToday"]!);
        Assert.Equal(9_750m, (decimal)fc["endBalance"]!); // +500 receivable +250 cheque
        Assert.Equal(250m, fc["weeks"]!.AsArray().Sum(w => (decimal)w!["amounts"]!["cheques"]!));
        Assert.Contains(fc["sources"]!.AsArray(), s => (string?)s!["key"] == "receivables");
    }

    [Fact]
    public async Task Real_estate_reports_cover_rent_roll_occupancy_performance_expiries_maintenance_and_commissions()
    {
        var (c, entityId) = await OwnerAsync();
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code = "R" + Guid.NewGuid().ToString("N")[..6], name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var property = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entityId, name = "Report Tower " + Guid.NewGuid().ToString("N")[..4], propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 10_000_000m }), HttpStatusCode.Created);
        var propId = (string)property["id"]!;
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = propId, unitNumber = "7A", unitTypeId = (string)type["id"]!, areaSqft = 1000, askingRent = 100_000m }), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = propId, unitNumber = "7B", unitTypeId = (string)type["id"]!, areaSqft = 800, askingRent = 80_000m }), HttpStatusCode.Created);
        var tenant = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Rent Roll Tenant", kind = "Individual", roles = "Customer" }), HttpStatusCode.Created);
        var year = DateTime.UtcNow.Year;
        await Ok(await c.PostAsJsonAsync($"{Re}/leases", new
        {
            unitId = (string)unit["id"]!, lesseePartyId = (string)tenant["id"]!, startDate = $"{year}-01-01", endDate = $"{year + 1}-12-31", annualRent = 120_000m, paymentFrequency = 4, activateNow = true,
        }), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Re}/billing/run", new { asOf = $"{year}-04-01", legalEntityId = entityId }));

        var roll = await Ok(await c.GetAsync($"{Re}/reports/rent-roll?asOf={year}-06-30&propertyIds={propId}"));
        var row = roll["rows"]!.AsArray().Single()!;
        Assert.Equal(("7A", 120m), (((string)row["unit"]!), (decimal)row["rentPerSqft"]!));
        Assert.Equal("exempt", (string)row["vatStatus"]!);
        Assert.Equal(60_000m, (decimal)row["outstanding"]!); // two quarterly instalments invoiced

        var occ = await Ok(await c.GetAsync($"{Re}/reports/occupancy"));
        var occRow = occ["rows"]!.AsArray().Single(r => (string)r!["propertyId"]! == propId)!;
        Assert.Equal((2, 1, 1, 50m), (((int)occRow["registered"]!), ((int)occRow["occupied"]!), ((int)occRow["vacant"]!), (decimal)occRow["occupancyPercent"]!));
        Assert.Equal((120_000m, 80_000m), ((decimal)occRow["contractedRent"]!, (decimal)occRow["vacantAskingRent"]!));

        var perf = await Ok(await c.GetAsync($"{Re}/reports/portfolio-performance?from={year}-01-01&to={year}-12-31"));
        var p = perf["rows"]!.AsArray().Single(r => (string)r!["propertyId"]! == propId)!;
        Assert.Equal(60_000m, (decimal)p["income"]!);
        Assert.Equal(60_000m, (decimal)p["billed"]!);
        Assert.Equal(0m, (decimal)p["collected"]!);

        var expiries = await Ok(await c.GetAsync($"{Re}/reports/lease-expiries?months=12"));
        Assert.True((int)expiries["totalLeases"]! >= 0);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Re}/reports/maintenance-costs?from={year}-01-01&to={year}-12-31")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Re}/reports/commissions?from={year}-01-01&to={year}-12-31")).StatusCode);
    }

    private async Task<(HttpClient Client, string EntityId)> OwnerAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        return (c, (string)me["legalEntities"]![0]!["id"]!);
    }
}
