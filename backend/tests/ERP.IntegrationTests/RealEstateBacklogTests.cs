using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Real estate backlog cards TASK-090 to TASK-094: billing after termination, lease input checks, atomic lease commands, deposit input checks, inspection links.</summary>
public class RealEstateBacklogTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Bank, JsonArray Accounts);

    private async Task<Ctx> OwnerAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        return new Ctx(c, (string)me["legalEntities"]![0]!["id"]!, (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!, accounts);
    }

    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<(string PropertyId, string TypeId)> PropertyAsync(Ctx x)
    {
        var type = await Ok(await x.C.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await x.C.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = x.Entity, name = U("Tower "), propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1_000_000m }), HttpStatusCode.Created);
        return ((string)prop["id"]!, (string)type["id"]!);
    }

    private async Task<string> UnitAsync(Ctx x, string propertyId, string typeId, string number, decimal rent = 120_000m)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Re}/units", new { propertyId, unitNumber = number, unitTypeId = typeId, areaSqft = 900, askingRent = rent }), HttpStatusCode.Created))["id"]!;

    private async Task<string> TenantAsync(Ctx x, string roles = "Customer")
        => (string)(await Ok(await x.C.PostAsJsonAsync("/api/v1/parties", new { name = U("Tenant "), kind = "Individual", roles }), HttpStatusCode.Created))["id"]!;

    private async Task<(string LeaseId, string UnitId, string PropertyId)> LeaseAsync(Ctx x, string start, string end, decimal rent = 120_000m, decimal deposit = 10_000m, bool activate = true)
    {
        var (propertyId, typeId) = await PropertyAsync(x);
        var unitId = await UnitAsync(x, propertyId, typeId, "1", rent);
        var tenant = await TenantAsync(x);
        var lease = await Ok(await x.C.PostAsJsonAsync($"{Re}/leases", new { unitId, lesseePartyId = tenant, startDate = start, endDate = end, annualRent = rent, paymentFrequency = 4, securityDepositAmount = deposit, activateNow = activate }), HttpStatusCode.Created);
        return ((string)lease["id"]!, unitId, propertyId);
    }

    [Fact]
    public async Task The_pro_rated_instalment_of_a_terminated_lease_can_still_be_invoiced()
    {
        var x = await OwnerAsync();
        var (leaseId, _, _) = await LeaseAsync(x, "2026-01-01", "2026-12-31"); // quarterly: 30,000 per instalment
        await Ok(await x.C.PostAsJsonAsync($"{Re}/leases/{leaseId}/terminate", new { date = "2026-02-14", reason = "Tenant left" })); // 45 of 90 days in the first period
        var billed = await Ok(await x.C.PostAsJsonAsync($"{Re}/billing/run", new { asOf = "2026-12-31", legalEntityId = x.Entity }));
        var schedule = (await Ok(await x.C.GetAsync($"{Re}/leases/{leaseId}/schedule"))).AsArray();
        var first = schedule.Single(i => (int)i!["sequence"]! == 1)!;
        Assert.Equal("invoiced", (string)first["status"]!);
        Assert.Equal(1, schedule.Count(i => (string)i!["status"]! == "invoiced"));
        Assert.True((int)billed["invoiced"]! >= 1);
        var invoice = await Ok(await x.C.GetAsync($"{Fin}/invoices/{(string)first["invoiceId"]!}"));
        Assert.Equal((decimal)first["amount"]!, (decimal)invoice["net"]!);
        Assert.True((decimal)first["amount"]! < 30_000m);
    }
}
