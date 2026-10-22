using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Acquisitions and funding: capitalisation, schedules, instalment payments and the cash forecast.</summary>
public class AcquisitionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<(HttpClient C, string Entity, string Bank, string PropertyId)> SetupAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var bank = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var property = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "Acq " + Guid.NewGuid().ToString("N")[..6], propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 68_000_000m }), HttpStatusCode.Created);
        return (c, entity, bank, (string)property["id"]!);
    }

    private static object Body(string entity, string property, string bank, object[]? funding = null, decimal price = 68_000_000m, decimal down = 20_400_000m)
        => new
        {
            legalEntityId = entity, propertyId = property, sellerName = "Bayside Developments PJSC", purchaseDate = "2026-06-01", contractReference = "SPA-014", purchasePrice = price, downPayment = down, transferFees = 2_720_000m,
            otherCosts = 150_000m, payFromAccountId = bank, funding = funding ?? new object[]
            {
                new { type = "bankMortgage", lender = "Abu Dhabi Commercial Bank", principal = 40_000_000m, annualRatePercent = 5.25m, termMonths = 240, firstDueDate = "2026-07-01" },
                new { type = "developerPlan", lender = "Bayside Developments PJSC", principal = 7_600_000m, annualRatePercent = 0m, termMonths = 24, firstDueDate = "2026-07-01" },
            },
        };

    [Fact]
    public async Task Completing_an_acquisition_capitalises_the_property_and_generates_the_schedules()
    {
        var (c, entity, bank, property) = await SetupAsync();
        var draft = await Ok(await c.PostAsJsonAsync($"{Re}/acquisitions", Body(entity, property, bank)), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        Assert.StartsWith("ACQ-", (string)draft["number"]!);
        Assert.Equal((70_870_000m, 23_270_000m, 47_600_000m, 0m), ((decimal)draft["totalCost"]!, (decimal)draft["cashAtCompletion"]!, (decimal)draft["financed"]!, (decimal)draft["unfunded"]!));
        Assert.Equal(269_537.67m, (decimal)draft["funding"]![0]!["monthlyPayment"]!);

        var done = await Ok(await c.PostAsync($"{Re}/acquisitions/{id}/complete", null));
        Assert.Equal("completed", (string)done["status"]!);
        Assert.Equal(240, done["funding"]![0]!["instalments"]!.AsArray().Count);

        var tb = await Ok(await c.GetAsync($"{Fin}/reports/trial-balance?to=2026-12-31&legalEntityId={entity}"));
        decimal Closing(string code) => (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
        Assert.True(Closing("1600") >= 70_870_000m);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Re}/acquisitions/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Re}/acquisitions/{id}", Body(entity, property, bank))).StatusCode); // financing is fixed
    }

    [Fact]
    public async Task Funding_must_cover_the_price_and_cash_needs_a_bank_account()
    {
        var (c, entity, bank, property) = await SetupAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/acquisitions", Body(entity, property, bank, down: 70_000_000m))).StatusCode); // down payment above the price
        var over = new object[] { new { type = "loan", lender = "L", principal = 60_000_000m, annualRatePercent = 4m, termMonths = 60, firstDueDate = "2026-07-01" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/acquisitions", Body(entity, property, bank, over))).StatusCode); // down + loan > price

        var short_ = await Ok(await c.PostAsJsonAsync($"{Re}/acquisitions", Body(entity, property, bank, new object[] { new { type = "loan", lender = "L", principal = 10_000_000m, annualRatePercent = 4m, termMonths = 60, firstDueDate = "2026-07-01" } })), HttpStatusCode.Created);
        Assert.Equal(37_600_000m, (decimal)short_["unfunded"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Re}/acquisitions/{(string)short_["id"]!}/complete", null)).StatusCode); // not fully funded

        var noBank = await Ok(await c.PostAsJsonAsync($"{Re}/acquisitions", new
        {
            legalEntityId = entity, propertyId = property, sellerName = "S", purchaseDate = "2026-06-01", purchasePrice = 1_000_000m, downPayment = 1_000_000m, transferFees = 0m, otherCosts = 0m,
        }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsync($"{Re}/acquisitions/{(string)noBank["id"]!}/complete", null)).StatusCode);
        Assert.Equal("cancelled", (string)(await Ok(await c.PostAsync($"{Re}/acquisitions/{(string)noBank["id"]!}/cancel", null)))["status"]!);
    }

    [Fact]
    public async Task Instalments_are_paid_in_order_splitting_principal_and_interest()
    {
        var (c, entity, bank, property) = await SetupAsync();
        var draft = await Ok(await c.PostAsJsonAsync($"{Re}/acquisitions", Body(entity, property, bank, new object[]
        {
            new { type = "bankMortgage", lender = "Test Bank", principal = 12_000_000m, annualRatePercent = 6m, termMonths = 12, firstDueDate = "2026-07-01" },
        }, price: 32_400_000m, down: 20_400_000m)), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        var done = await Ok(await c.PostAsync($"{Re}/acquisitions/{id}/complete", null));
        var schedule = done["funding"]![0]!["instalments"]!.AsArray();
        var first = schedule[0]!; var second = schedule[1]!;

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/acquisitions/instalments/{(string)second["id"]!}/pay", new { bankAccountId = bank, date = "2026-08-01" })).StatusCode); // out of order
        var paid = await Ok(await c.PostAsJsonAsync($"{Re}/acquisitions/instalments/{(string)first["id"]!}/pay", new { bankAccountId = bank, date = "2026-07-01" }));
        Assert.Equal(1, (int)paid["paid"]!);
        Assert.Equal(12_000_000m - (decimal)first["principal"]!, (decimal)paid["outstanding"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/acquisitions/instalments/{(string)first["id"]!}/pay", new { bankAccountId = bank, date = "2026-07-02" })).StatusCode);

        var listed = (await Ok(await c.GetAsync($"{Re}/acquisitions/funding"))).AsArray();
        Assert.Contains(listed, s => (string?)s!["lender"] == "Test Bank" && (int)s["paid"]! == 1);
        var overview = await Ok(await c.GetAsync($"{Re}/acquisitions/overview"));
        Assert.True((decimal)overview["consideration"]! >= 32_400_000m);

        // unpaid instalments appear as financing outflows in the cash forecast
        var fc = await Ok(await c.GetAsync($"{Fin}/reports/cash-forecast?weeks=4&legalEntityId={entity}"));
        Assert.Contains(fc["sources"]!.AsArray(), s => (string?)s!["key"] == "financing");
    }
}
