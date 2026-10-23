using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Preventive maintenance schedules and the lease renewals workbench.</summary>
public class RealEstateOpsTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string U(string p) => p + Guid.NewGuid().ToString("N")[..6];

    private async Task<(HttpClient C, string Entity, string PropertyId, string UnitId)> SetupAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code = U("T"), name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = U("Tower "), propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1_000_000m }), HttpStatusCode.Created);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "3", unitTypeId = (string)type["id"]!, areaSqft = 900, askingRent = 130_000m }), HttpStatusCode.Created);
        return (c, entity, (string)prop["id"]!, (string)unit["id"]!);
    }

    [Fact]
    public async Task A_due_schedule_raises_one_work_order_and_moves_to_its_next_occurrence()
    {
        var (c, _, propertyId, _) = await SetupAsync();
        var due = DateTime.UtcNow.AddDays(3).ToString("yyyy-MM-dd");
        var created = await Ok(await c.PostAsJsonAsync($"{Re}/pm-schedules", new { propertyId, title = "Lift inspection", frequencyMonths = 3, nextDue = due, leadDays = 7, category = "lift", estimatedCost = 500m }), HttpStatusCode.Created);
        var id = (string)created["id"]!;

        var first = await Ok(await c.PostAsync($"{Re}/pm-schedules/run", null));
        Assert.True((int)first["workOrdersCreated"]! >= 1);
        Assert.Equal(0, (int)(await Ok(await c.PostAsync($"{Re}/pm-schedules/run", null)))["workOrdersCreated"]!); // idempotent

        var orders = (await Ok(await c.GetAsync($"{Re}/work-orders?search={Uri.EscapeDataString("Lift inspection")}&pageSize=50"))).AsArray();
        Assert.Contains(orders, o => (string)o!["propertyId"]! == propertyId && (string)o["reportedBy"]! == "Preventive maintenance");
        var schedule = (await Ok(await c.GetAsync($"{Re}/pm-schedules?pageSize=100"))).AsArray().Single(s => (string)s!["id"]! == id)!;
        Assert.Equal(DateTime.UtcNow.AddDays(3).AddMonths(3).ToString("yyyy-MM-dd"), (string)schedule["nextDue"]!);
    }

    [Fact]
    public async Task A_schedule_must_belong_to_a_valid_property_and_unit()
    {
        var (c, _, propertyId, _) = await SetupAsync();
        var bad = await c.PostAsJsonAsync($"{Re}/pm-schedules", new { propertyId = Guid.NewGuid(), title = "x", frequencyMonths = 1, nextDue = "2027-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var wrongUnit = await c.PostAsJsonAsync($"{Re}/pm-schedules", new { propertyId, unitId = Guid.NewGuid(), title = "x", frequencyMonths = 1, nextDue = "2027-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongUnit.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/pm-schedules", new { propertyId, title = "", frequencyMonths = 0, nextDue = "2027-01-01" })).StatusCode);
    }

    [Fact]
    public async Task The_renewals_workbench_lists_expiring_leases_and_creates_drafts_with_a_rent_uplift()
    {
        var (c, _, _, unitId) = await SetupAsync();
        var tenant = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = U("Tenant "), kind = "Individual", roles = "Customer" }), HttpStatusCode.Created);
        var start = DateTime.UtcNow.AddMonths(-10).ToString("yyyy-MM-dd");
        var end = DateTime.UtcNow.AddDays(40).ToString("yyyy-MM-dd");
        var lease = await Ok(await c.PostAsJsonAsync($"{Re}/leases", new { unitId, lesseePartyId = (string)tenant["id"]!, startDate = start, endDate = end, annualRent = 100_000m, paymentFrequency = 4, securityDepositAmount = 0, activateNow = true }), HttpStatusCode.Created);
        var leaseId = (string)lease["id"]!;

        var bench = await Ok(await c.GetAsync($"{Re}/renewals?days=90"));
        var row = bench["rows"]!.AsArray().Single(r => (string)r!["leaseId"]! == leaseId)!;
        Assert.Equal("due", (string)row["state"]!);
        Assert.Equal(30m, (decimal)row["marketVariancePercent"]!); // asking 130k vs 100k

        var result = (await Ok(await c.PostAsJsonAsync($"{Re}/renewals/drafts", new { leaseIds = new[] { leaseId }, upliftPercent = 5 }))).AsArray().Single()!;
        Assert.Null((string?)result["error"]);
        var renewal = await Ok(await c.GetAsync($"{Re}/leases/{(string)result["renewalLeaseId"]!}"));
        Assert.Equal(105_000m, (decimal)renewal["annualRent"]!);
        Assert.Equal("draft", (string)renewal["status"]!);

        var after = (await Ok(await c.GetAsync($"{Re}/renewals?days=90")))["rows"]!.AsArray().Single(r => (string)r!["leaseId"]! == leaseId)!;
        Assert.Equal("draftRenewal", (string)after["state"]!);
        // a second attempt reports the conflict per lease instead of failing the batch
        var again = (await Ok(await c.PostAsJsonAsync($"{Re}/renewals/drafts", new { leaseIds = new[] { leaseId }, upliftPercent = 5 }))).AsArray().Single()!;
        Assert.NotNull((string?)again["error"]);
    }
}

/// <summary>Unit/property inspections with checklists and repair work orders.</summary>
public class InspectionTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private async Task<(HttpClient C, string PropertyId, string UnitId)> SetupAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var code = "T" + Guid.NewGuid().ToString("N")[..6];
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code, name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = (string)me["legalEntities"]![0]!["id"]!, name = "Insp " + code, propertyType = "residential", ownershipType = "owned", totalUnits = 0, indicativeValue = 1m }), HttpStatusCode.Created);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "9", unitTypeId = (string)type["id"]!, areaSqft = 800, askingRent = 80_000m }), HttpStatusCode.Created);
        return (c, (string)prop["id"]!, (string)unit["id"]!);
    }

    [Fact]
    public async Task Completing_an_inspection_totals_damage_and_raises_repair_work_orders_once()
    {
        var (c, propertyId, unitId) = await SetupAsync();
        var created = await Ok(await c.PostAsJsonAsync($"{Re}/inspections", new
        {
            propertyId, unitId, kind = "moveOut", inspectionDate = "2026-06-01", inspector = "Vikram",
            items = new object[] { new { area = "Living room", condition = "good" }, new { area = "Kitchen cabinet", condition = "damaged", repairCost = 450m, notes = "Door broken" }, new { area = "Bathroom", condition = "poor", repairCost = 150m } },
        }), HttpStatusCode.Created);
        var id = (string)created["id"]!;
        Assert.Equal(600m, (decimal)created["damageCost"]!);

        var edited = await Ok(await c.PutAsJsonAsync($"{Re}/inspections/{id}", new
        {
            propertyId, unitId, kind = "moveOut", inspectionDate = "2026-06-01", inspector = "Vikram",
            items = new object[] { new { area = "Kitchen cabinet", condition = "damaged", repairCost = 500m } },
        }));
        Assert.Equal(500m, (decimal)edited["damageCost"]!);

        var done = await Ok(await c.PostAsJsonAsync($"{Re}/inspections/{id}/complete", new { raiseWorkOrders = true }));
        Assert.Equal("completed", (string)done["status"]!);
        var item = Assert.Single(done["items"]!.AsArray())!;
        var workOrderId = (string?)item["workOrderId"];
        Assert.NotNull(workOrderId);
        var order = await Ok(await c.GetAsync($"{Re}/work-orders/{workOrderId}"));
        Assert.Equal(500m, (decimal)order["estimatedCost"]!);
        Assert.Equal("high", (string)order["priority"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/inspections/{id}/complete", new { raiseWorkOrders = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Re}/inspections/{id}", new { propertyId, unitId, kind = "moveOut", inspectionDate = "2026-06-01", items = Array.Empty<object>() })).StatusCode);
    }

    [Fact]
    public async Task Inspections_validate_their_property_unit_and_checklist()
    {
        var (c, propertyId, _) = await SetupAsync();
        var body = (object unit, object[] items) => new { propertyId, unitId = unit, kind = "routine", inspectionDate = "2026-06-01", items };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/inspections", body(Guid.NewGuid(), []))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/inspections", body(null!, [new { area = "", condition = "good" }]))).StatusCode);
        var empty = await Ok(await c.PostAsJsonAsync($"{Re}/inspections", body(null!, [])), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/inspections/{(string)empty["id"]!}/complete", new { raiseWorkOrders = false })).StatusCode); // empty checklist
        Assert.Contains((await Ok(await c.GetAsync($"{Re}/inspections?pageSize=100"))).AsArray(), r => (string)r!["id"]! == (string)empty["id"]!);
    }
}

/// <summary>Owner statements for properties managed on behalf of an owner.</summary>
public class OwnerStatementTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    [Fact]
    public async Task The_statement_nets_collected_rent_against_the_management_fee_and_maintenance()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        var entity = (string)me["legalEntities"]![0]!["id"]!;
        var accounts = (await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray();
        var bank = (string)accounts.First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        var code = "O" + Guid.NewGuid().ToString("N")[..6];
        var owner = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Owner " + code, kind = "Individual", roles = "Vendor" }), HttpStatusCode.Created);
        var tenant = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Tenant " + code, kind = "Individual", roles = "Customer" }), HttpStatusCode.Created);
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code, name = "Flat", defaultUsage = "residential" }), HttpStatusCode.Created);
        var prop = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "Managed " + code, propertyType = "residential", ownershipType = "managedForOwner", ownerPartyId = (string)owner["id"]!, managementFeePercent = 5, totalUnits = 0, indicativeValue = 1m }), HttpStatusCode.Created);
        Assert.Equal(5m, (decimal)prop["managementFeePercent"]!);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)prop["id"]!, unitNumber = "1", unitTypeId = (string)type["id"]!, areaSqft = 800, askingRent = 120_000m }), HttpStatusCode.Created);
        var start = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd");
        await Ok(await c.PostAsJsonAsync($"{Re}/leases", new { unitId = (string)unit["id"]!, lesseePartyId = (string)tenant["id"]!, startDate = start, endDate = DateTime.UtcNow.AddMonths(11).ToString("yyyy-MM-dd"), annualRent = 120_000m, paymentFrequency = 12, activateNow = true }), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Re}/billing/run", new { asOf = DateTime.UtcNow.ToString("yyyy-MM-dd"), legalEntityId = entity }));
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        await Ok(await c.PostAsJsonAsync($"{Fin}/receipts", new { legalEntityId = entity, partyId = (string)tenant["id"]!, date = today, method = "bankTransfer", bankAccountId = bank, amount = 10_000m, autoAllocate = true }), HttpStatusCode.Created);

        var wo = await Ok(await c.PostAsJsonAsync($"{Re}/work-orders", new { propertyId = (string)prop["id"]!, issue = "Repaint", priority = "normal", technician = "Crew", estimatedCost = 1500m }), HttpStatusCode.Created);
        var id = (string)wo["id"]!;
        await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "assigned" }));
        await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "inProgress" }));
        await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "completed", actualCost = 1200m }));

        var from = DateTime.UtcNow.AddDays(-5).ToString("yyyy-MM-dd"); var to = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");
        var st = await Ok(await c.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={(string)owner["id"]!}&from={from}&to={to}"));
        Assert.Equal(10_000m, (decimal)st["rentCollected"]!);
        Assert.Equal(500m, (decimal)st["managementFee"]!);
        Assert.Equal(1_200m, (decimal)st["maintenanceCosts"]!);
        Assert.Equal(8_300m, (decimal)st["netToOwner"]!);

        var empty = await Ok(await c.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={(string)owner["id"]!}&from=2020-01-01&to=2020-01-31"));
        Assert.Equal(0m, (decimal)empty["netToOwner"]!);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Re}/reports/owner-statement?ownerPartyId={(string)owner["id"]!}&from=2026-02-01&to=2026-01-01")).StatusCode);
    }
}
