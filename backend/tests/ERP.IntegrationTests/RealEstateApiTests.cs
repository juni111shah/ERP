using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>The real estate module end to end on the seeded UAE real-estate workspace ("demo-re").</summary>
public class RealEstateApiTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Re = "/api/v1/m/realestate";
    private int _seed;

    private async Task<(HttpClient Client, string EntityId)> OwnerAsync()
    {
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var me = await ApiFactory.Json(await c.GetAsync("/api/v1/me"));
        return (c, (string)me["legalEntities"]![0]!["id"]!);
    }

    private string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 8, 30)];

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    /// <summary>Creates a unit type, a property and one unit; returns their ids.</summary>
    private async Task<(string PropertyId, string UnitId, string TenantId)> SetupAsync(HttpClient c, string entityId, string usage = "residential")
    {
        var type = await Ok(await c.PostAsJsonAsync($"{Re}/unit-types", new { code = Unique("T"), name = "Type", defaultUsage = usage, defaultBedrooms = 2 }), HttpStatusCode.Created);
        var property = await Ok(await c.PostAsJsonAsync($"{Re}/properties", new
        {
            legalEntityId = entityId, name = "Marina Tower " + (++_seed), propertyType = "residential", ownershipType = "owned", region = "Dubai", community = "Dubai Marina",
            totalUnits = 0, indicativeValue = 50_000_000m,
        }), HttpStatusCode.Created);
        var unit = await Ok(await c.PostAsJsonAsync($"{Re}/units", new { propertyId = (string)property["id"]!, unitNumber = "1406", unitTypeId = (string)type["id"]!, areaSqft = 950, askingRent = 110_000m }), HttpStatusCode.Created);
        var tenant = await Ok(await c.PostAsJsonAsync("/api/v1/parties", new { name = "Tenant " + Guid.NewGuid().ToString("N")[..6], kind = "Individual", roles = "Customer" }), HttpStatusCode.Created);
        return ((string)property["id"]!, (string)unit["id"]!, (string)tenant["id"]!);
    }

    private static object LeaseBody(string unitId, string tenantId, string start = "2026-11-01", string end = "2027-10-31", decimal rent = 120_000m, object? customFields = null, bool activate = false)
        => new { unitId, lesseePartyId = tenantId, startDate = start, endDate = end, annualRent = rent, paymentFrequency = 4, securityDepositAmount = 10_000m, customFields, activateNow = activate };

    [Fact]
    public async Task Property_and_units_are_numbered_validated_and_keep_total_units_in_step()
    {
        var (c, entity) = await OwnerAsync();
        var (propertyId, unitId, _) = await SetupAsync(c, entity);

        var summary = await Ok(await c.GetAsync($"{Re}/properties/{propertyId}"));
        Assert.StartsWith("PROP-", (string)summary["property"]!["code"]!);
        Assert.Equal(1, (int)summary["registeredUnits"]!);
        Assert.Equal(1, (int)summary["property"]!["totalUnits"]!); // grown to the registered count
        Assert.False((bool)summary["unitsNotFullyRegistered"]!);

        var unit = await Ok(await c.GetAsync($"{Re}/units/{unitId}"));
        Assert.Equal("vacant", (string?)unit["status"]);
        Assert.Equal("residential", (string?)unit["usage"]); // default from the unit type

        // Duplicate unit number in the same property.
        var dup = await c.PostAsJsonAsync($"{Re}/units", new { propertyId, unitNumber = "1406", unitTypeId = (string)unit["unitTypeId"]!, areaSqft = 1, askingRent = 1 });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // Rent history records the initial asking rent and each change.
        await Ok(await c.PutAsJsonAsync($"{Re}/units/{unitId}", new { propertyId, unitNumber = "1406", unitTypeId = (string)unit["unitTypeId"]!, areaSqft = 950, askingRent = 125_000m }));
        var history = (await Ok(await c.GetAsync($"{Re}/units/{unitId}/rent-history"))).AsArray();
        Assert.Single(history); // both changes happened today: the second replaces the first
        Assert.Equal(125_000m, (decimal)history[0]!["annualRent"]!);

        // Bulk create: 2 floors x 3 units.
        var bulk = (await Ok(await c.PostAsJsonAsync($"{Re}/units/bulk", new { propertyId, unitTypeId = (string)unit["unitTypeId"]!, fromFloor = 2, toFloor = 3, unitsPerFloor = 3, areaSqft = 800, askingRent = 90_000m }), HttpStatusCode.Created)).AsArray();
        Assert.Equal(["201", "202", "203", "301", "302", "303"], bulk.Select(u => (string)u!["unitNumber"]!));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/units/bulk", new { propertyId, unitTypeId = (string)unit["unitTypeId"]!, fromFloor = 3, toFloor = 3, unitsPerFloor = 1, areaSqft = 1, askingRent = 1 })).StatusCode);
        Assert.Equal(7, (int)(await Ok(await c.GetAsync($"{Re}/properties/{propertyId}")))["registeredUnits"]!);

        // Standard paging / search / filter on the enriched units list.
        var page = await Ok(await c.GetAsync($"{Re}/units?filter=propertyId:eq:{propertyId}&filter=status:eq:vacant&sort=unitNumber&pageSize=3"));
        Assert.Equal(3, page.AsArray().Count);
        Assert.Equal("1406", (string?)page[0]!["unitNumber"]);
        Assert.Equal("Type", (string?)page[0]!["unitTypeName"]);
    }

    [Fact]
    public async Task Property_and_unit_validation_errors_are_keyed_by_field()
    {
        var (c, entity) = await OwnerAsync();
        var bad = await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "", propertyType = "residential", ownershipType = "managedForOwner", totalUnits = -1, indicativeValue = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ApiFactory.Envelope(bad))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("name") && errors.ContainsKey("ownerPartyId") && errors.ContainsKey("totalUnits"), string.Join(",", errors.Select(e => e.Key)));

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = Guid.NewGuid(), name = "X", propertyType = "land", ownershipType = "owned", totalUnits = 0, indicativeValue = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/unit-types", new { code = "dup", name = "A", defaultUsage = "commercial" })).StatusCode is HttpStatusCode.Created
            ? (await c.PostAsJsonAsync($"{Re}/unit-types", new { code = "DUP", name = "B", defaultUsage = "commercial" })).StatusCode
            : HttpStatusCode.Conflict); // codes are unique case-insensitively (stored upper-case)
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"{Re}/properties?filter=notAField:eq:1")).StatusCode);
    }

    [Fact]
    public async Task Lease_lifecycle_draft_activate_renew_terminate()
    {
        var (c, entity) = await OwnerAsync();
        var (propertyId, unitId, tenantId) = await SetupAsync(c, entity);

        // UAE pack fields are available on the lease form.
        var form = (await Ok(await c.GetAsync("/api/v1/custom-fields/form/realestate.lease"))).AsArray();
        Assert.Contains(form, d => (string?)d!["key"] == "cf_ae_registration_no");

        var draft = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, customFields: new { cf_ae_registration_authority = "ejari", cf_ae_registration_no = "EJ-2026-1" })), HttpStatusCode.Created);
        var leaseId = (string)draft["id"]!;
        Assert.Equal("draft", (string?)draft["status"]);
        Assert.StartsWith("LSE-", (string)draft["number"]!);
        Assert.Equal("exempt", (string?)draft["suggestedVat"]); // residential unit
        Assert.Equal(120_000m, (decimal)draft["rentForTerm"]!);
        Assert.Equal("EJ-2026-1", (string?)draft["customFields"]!["cf_ae_registration_no"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]); // a draft does not occupy

        // Only drafts are editable.
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Re}/leases/{leaseId}", LeaseBody(unitId, tenantId, rent: 132_000m))).StatusCode);

        var active = await Ok(await c.PostAsync($"{Re}/leases/{leaseId}/activate", null));
        Assert.Equal("active", (string?)active["status"]);
        var schedule = active["schedule"]!.AsArray();
        Assert.Equal(4, schedule.Count);
        Assert.All(schedule, i => Assert.Equal(33_000m, (decimal)i!["amount"]!));
        Assert.Equal("2026-11-01", (string?)schedule[0]!["dueDate"]);
        Assert.Equal("occupied", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Re}/leases/{leaseId}", LeaseBody(unitId, tenantId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Re}/leases/{leaseId}/activate", null)).StatusCode);

        // The unit is taken: a second lease cannot be created for it.
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, "2027-01-01", "2027-12-31"))).StatusCode);

        // Renewal: a draft starting the day after the end; activating it renews the old lease and keeps the unit occupied.
        var renewal = await Ok(await c.PostAsJsonAsync($"{Re}/leases/{leaseId}/renew", new { annualRent = 138_000m }), HttpStatusCode.Created);
        Assert.Equal("2027-11-01", (string?)renewal["startDate"]);
        Assert.Equal(leaseId, (string?)renewal["renewalOfLeaseId"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/leases/{leaseId}/renew", new { })).StatusCode);
        var renewalId = (string)renewal["id"]!;
        Assert.Equal("active", (string?)(await Ok(await c.PostAsync($"{Re}/leases/{renewalId}/activate", null)))["status"]);
        var old = await Ok(await c.GetAsync($"{Re}/leases/{leaseId}"));
        Assert.Equal("renewed", (string?)old["status"]);
        Assert.Equal(renewalId, (string?)old["renewedAsLeaseId"]);
        Assert.Equal("occupied", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]);

        // Early termination: later instalments are cancelled, the date's instalment is pro-rated, the unit is vacated.
        var result = await Ok(await c.PostAsJsonAsync($"{Re}/leases/{renewalId}/terminate", new { date = "2028-01-15", reason = "Tenant relocation" }));
        Assert.Equal("terminated", (string?)result["lease"]!["status"]);
        Assert.Equal(3, (int)result["cancelledInstalments"]!);
        var rows = result["lease"]!["schedule"]!.AsArray();
        Assert.Equal("scheduled", (string?)rows[0]!["status"]);
        Assert.Equal("2028-01-15", (string?)rows[0]!["periodTo"]);
        Assert.True((decimal)rows[0]!["amount"]! < 34_500m); // pro-rated: 76 of 92 days
        Assert.All(rows.Skip(1), r => Assert.Equal("cancelled", (string?)r!["status"]));
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]);
        Assert.Equal(0, (int)(await Ok(await c.GetAsync($"{Re}/properties/{propertyId}")))["occupiedUnits"]!);

        // Lists are enriched and filterable (e.g. leases of this unit).
        var list = await Ok(await c.GetAsync($"{Re}/leases?filter=unitId:eq:{unitId}&sort=startDate"));
        Assert.Equal(2, list.AsArray().Count);
        Assert.Equal("1406", (string?)list[0]!["unitNumber"]);
        Assert.StartsWith("Tenant ", (string?)list[0]!["tenantName"]);
    }

    [Fact]
    public async Task Lease_input_is_validated_and_drafts_can_be_cancelled()
    {
        var (c, entity) = await OwnerAsync();
        var (_, unitId, tenantId) = await SetupAsync(c, entity);

        var bad = await c.PostAsJsonAsync($"{Re}/leases", new { unitId, lesseePartyId = tenantId, startDate = "2026-11-01", endDate = "2026-10-01", annualRent = 0, paymentFrequency = 5, securityDepositAmount = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ApiFactory.Envelope(bad))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("endDate") && errors.ContainsKey("annualRent") && errors.ContainsKey("paymentFrequency") && errors.ContainsKey("securityDepositAmount"), string.Join(",", errors.Select(e => e.Key)));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(Guid.NewGuid().ToString(), tenantId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, Guid.NewGuid().ToString()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, customFields: new { cf_unknown = 1 }))).StatusCode);

        var draft = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId)), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        Assert.Equal("cancelled", (string?)(await Ok(await c.PostAsync($"{Re}/leases/{id}/cancel", null)))["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Re}/leases/{id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/leases/{id}/terminate", new { date = "2027-01-01", reason = "x" })).StatusCode);

        // "Activate now" creates and activates in one call; a commercial unit suggests standard-rated VAT.
        var (_, commercialUnit, _) = await SetupAsync(c, entity, "commercial");
        var now = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(commercialUnit, tenantId, activate: true)), HttpStatusCode.Created);
        Assert.Equal("active", (string?)now["status"]);
        Assert.Equal("standard", (string?)now["suggestedVat"]);
    }

    [Fact]
    public async Task Daily_job_ends_expired_leases_holds_over_flagged_ones_and_releases_lapsed_reservations()
    {
        var (c, entity) = await OwnerAsync();
        var (_, endedUnit, tenantId) = await SetupAsync(c, entity);
        var (_, heldUnit, _) = await SetupAsync(c, entity);
        var (_, reservedUnit, _) = await SetupAsync(c, entity);

        var ended = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(endedUnit, tenantId, "2024-01-01", "2024-12-31", activate: true)), HttpStatusCode.Created);
        var held = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(heldUnit, tenantId, "2024-01-01", "2024-12-31", activate: true)), HttpStatusCode.Created);
        await Ok(await c.PostAsJsonAsync($"{Re}/leases/{(string)held["id"]!}/holding-over", new { holdingOver = true }));
        await Ok(await c.PostAsJsonAsync($"{Re}/units/{reservedUnit}/status", new { action = "reserve", until = "2024-06-01" }));

        var run = await Ok(await c.PostAsync($"{Re}/leases/process-expiries", null));
        Assert.True((int)run["ended"]! >= 1 && (int)run["heldOver"]! >= 1 && (int)run["reservationsReleased"]! >= 1);
        Assert.Equal("ended", (string?)(await Ok(await c.GetAsync($"{Re}/leases/{(string)ended["id"]!}")))["status"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{endedUnit}")))["status"]);
        Assert.Equal("holdingOver", (string?)(await Ok(await c.GetAsync($"{Re}/leases/{(string)held["id"]!}")))["status"]);
        Assert.Equal("occupied", (string?)(await Ok(await c.GetAsync($"{Re}/units/{heldUnit}")))["status"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{reservedUnit}")))["status"]);

        // Idempotent, and the held-over lease closes on move-out.
        var again = await Ok(await c.PostAsync($"{Re}/leases/process-expiries", null));
        Assert.Equal(0, (int)again["ended"]! + (int)again["reservationsReleased"]!);
        Assert.Equal("ended", (string?)(await Ok(await c.PostAsync($"{Re}/leases/{(string)held["id"]!}/move-out", null)))["status"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{heldUnit}")))["status"]);
    }

    [Fact]
    public async Task Unit_status_follows_the_lifecycle()
    {
        var (c, entity) = await OwnerAsync();
        var (_, unitId, tenantId) = await SetupAsync(c, entity);

        Assert.Equal("reserved", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/units/{unitId}/status", new { action = "reserve", until = "2099-01-01" })))["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/units/{unitId}/status", new { action = "reserve" })).StatusCode);
        // A reserved unit can still be let; activation occupies it.
        await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, activate: true)), HttpStatusCode.Created);
        Assert.Equal("occupied", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/units/{unitId}/status", new { action = "makeUnavailable", reason = "Renovation" })).StatusCode);

        var (_, other, _) = await SetupAsync(c, entity);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/units/{other}/status", new { action = "makeUnavailable" })).StatusCode); // reason required
        Assert.Equal("unavailable", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/units/{other}/status", new { action = "makeUnavailable", reason = "Renovation" })))["status"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/units/{other}/status", new { action = "makeAvailable" })))["status"]);
    }

    [Fact]
    public async Task Work_orders_move_through_their_lifecycle_and_unblock_units_parked_for_repairs()
    {
        var (c, entity) = await OwnerAsync();
        var (propertyId, unitId, tenantId) = await SetupAsync(c, entity);
        var lease = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, activate: true)), HttpStatusCode.Created);

        var wo = await Ok(await c.PostAsJsonAsync($"{Re}/work-orders", new { propertyId, unitId, issue = "AC not cooling", priority = "high", category = "HVAC", estimatedCost = 450 }), HttpStatusCode.Created);
        var id = (string)wo["id"]!;
        Assert.StartsWith("WO-", (string)wo["number"]!);
        Assert.Equal("new", (string?)wo["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "assigned" })).StatusCode); // nobody assigned
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "closed" })).StatusCode); // cannot skip

        await Ok(await c.PutAsJsonAsync($"{Re}/work-orders/{id}", new { propertyId, unitId, issue = "AC not cooling", priority = "high", category = "HVAC", technician = "Ali", estimatedCost = 450 }));
        Assert.Equal("assigned", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "assigned" })))["status"]);

        // Terminating the lease while the work order is open parks the unit as unavailable.
        await Ok(await c.PostAsJsonAsync($"{Re}/leases/{(string)lease["id"]!}/terminate", new { date = "2027-01-31", reason = "Tenant relocation" }));
        var parked = await Ok(await c.GetAsync($"{Re}/units/{unitId}"));
        Assert.Equal("unavailable", (string?)parked["status"]);
        Assert.StartsWith("Repairs pending", (string?)parked["unavailableReason"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/units/{unitId}/status", new { action = "makeAvailable" })).StatusCode); // work order still open

        await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "inProgress" }));
        var done = await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "completed", actualCost = 520, resolutionNotes = "Regassed" }));
        Assert.Equal(520m, (decimal)done["actualCost"]!);
        Assert.NotNull(done["completedOn"]);
        Assert.Equal("vacant", (string?)(await Ok(await c.GetAsync($"{Re}/units/{unitId}")))["status"]); // last open work order is done
        Assert.Equal("closed", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/work-orders/{id}/status", new { status = "closed" })))["status"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Re}/work-orders/{id}", new { propertyId, issue = "x", priority = "low" })).StatusCode);

        var open = await Ok(await c.GetAsync($"{Re}/work-orders?filter=propertyId:eq:{propertyId}&filter=status:eq:closed"));
        Assert.Single(open.AsArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync($"{Re}/work-orders", new { propertyId, unitId = Guid.NewGuid(), issue = "x", priority = "low" })).StatusCode);
    }

    [Fact]
    public async Task Deals_compute_commission_and_stop_before_finance_stages()
    {
        var (c, entity) = await OwnerAsync();
        var deal = await Ok(await c.PostAsJsonAsync($"{Re}/deals", new
        {
            legalEntityId = entity, dealType = "sale", clientName = "Mr Buyer", dealDate = "2026-10-01", dealValue = 1_234_567.89m, commissionRatePercent = 2m,
            agentSharePercent = 60m, teamLeadSharePercent = 10m, unitReference = "Listing 77",
        }), HttpStatusCode.Created);
        var id = (string)deal["id"]!;
        Assert.StartsWith("DEAL-", (string)deal["number"]!);
        Assert.Equal(24_691.36m, (decimal)deal["grossCommission"]!);
        Assert.Equal(14_814.82m, (decimal)deal["agentCommission"]!);
        Assert.Equal(2_469.14m, (decimal)deal["teamLeadCommission"]!);
        Assert.Equal("lead", (string?)deal["stage"]);

        var bad = await c.PostAsJsonAsync($"{Re}/deals", new { legalEntityId = entity, dealType = "lease", clientName = "X", dealDate = "2026-10-01", dealValue = 1, commissionRatePercent = 5m, agentSharePercent = 80m, teamLeadSharePercent = 30m });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        foreach (var stage in new[] { "viewing", "negotiation", "agreed" })
            Assert.Equal(stage, (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage })))["stage"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage = "invoiced" })).StatusCode); // needs the Finance module
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage = "lost" })).StatusCode); // reason required
        Assert.Equal("lost", (string?)(await Ok(await c.PostAsJsonAsync($"{Re}/deals/{id}/stage", new { stage = "lost", lostReason = "Chose another agency" })))["stage"]);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Re}/deals/{id}", new { legalEntityId = entity, dealType = "sale", clientName = "Mr Buyer", dealDate = "2026-10-01", dealValue = 1, commissionRatePercent = 1m })).StatusCode);
        Assert.Contains((await Ok(await c.GetAsync($"{Re}/deals?filter=stage:eq:lost"))).AsArray(), d => (string?)d!["id"] == id);
    }

    [Fact]
    public async Task Role_templates_limit_what_each_user_can_do_and_a_disabled_module_is_closed()
    {
        var (owner, entity) = await OwnerAsync();
        var (propertyId, unitId, tenantId) = await SetupAsync(owner, entity);
        var roles = (await Ok(await owner.GetAsync("/api/v1/roles"))).AsArray();
        var facilities = (string)roles.First(r => (string?)r!["name"] == "Facilities manager")!["id"]!;
        var email = $"fac{Guid.NewGuid():N}"[..14] + "@demo.local";
        await Ok(await owner.PostAsJsonAsync("/api/v1/users", new { email, fullName = "Fatima Facilities", temporaryPassword = "Temp#Passw0rd1", roleIds = new[] { facilities } }), HttpStatusCode.Created);

        var (fm, _) = await f.SignInAsync(email, "Temp#Passw0rd1");
        Assert.Equal(HttpStatusCode.OK, (await fm.GetAsync($"{Re}/properties")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fm.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fm.PostAsJsonAsync($"{Re}/properties", new { legalEntityId = entity, name = "Nope", propertyType = "land", ownershipType = "owned", totalUnits = 0, indicativeValue = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fm.PostAsJsonAsync($"{Re}/units/{unitId}/status", new { action = "reserve" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fm.GetAsync($"{Re}/deals")).StatusCode);
        var wo = await Ok(await fm.PostAsJsonAsync($"{Re}/work-orders", new { propertyId, unitId, issue = "Leaking tap", priority = "normal", technician = "Sam" }), HttpStatusCode.Created);
        Assert.Equal("assigned", (string?)(await Ok(await fm.PostAsJsonAsync($"{Re}/work-orders/{(string)wo["id"]!}/status", new { status = "assigned" })))["status"]);

        // A workspace without the module gets 403 module-disabled and none of its data.
        var (generic, _) = await f.SignInAsync(ApiFactory.AdminEmail);
        var blocked = await generic.GetAsync($"{Re}/properties");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("module-disabled", await blocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Country_pack_fields_for_real_estate_exist_only_in_the_pack_country()
    {
        var (c, _) = await OwnerAsync();
        var ae = (await Ok(await c.GetAsync("/api/v1/custom-fields/form/realestate.unit"))).AsArray();
        Assert.Contains(ae, d => (string?)d!["key"] == "cf_ae_premise_no");
        var property = (await Ok(await c.GetAsync("/api/v1/custom-fields/form/realestate.property"))).AsArray();
        Assert.Contains(property, d => (string?)d!["key"] == "cf_ae_title_deed_no");

        var key = "re-pk-" + Guid.NewGuid().ToString("N")[..6];
        await Ok(await f.Client().PostAsJsonAsync("/api/v1/platform/onboarding", new
        {
            workspaceName = "PK Estates", signInKey = key, industry = "realestate", countryCode = "PK", baseCurrency = "PKR", timeZone = "Asia/Karachi",
            adminEmail = $"admin+{key}@example.test", adminName = "PK Admin", adminPassword = "Qa#Passw0rd1",
        }), HttpStatusCode.Created);
        var (pk, _) = await f.SignInAsync($"admin+{key}@example.test", "Qa#Passw0rd1");
        var pkLease = (await Ok(await pk.GetAsync("/api/v1/custom-fields/form/realestate.lease"))).AsArray();
        Assert.DoesNotContain(pkLease, d => ((string?)d!["key"])?.StartsWith("cf_ae_") == true);
    }

    [Fact]
    public async Task Billing_run_invoices_due_instalments_once_and_voiding_releases_them()
    {
        var (c, entityId) = await OwnerAsync();
        var (_, unitId, tenantId) = await SetupAsync(c, entityId);
        var lease = await Ok(await c.PostAsJsonAsync($"{Re}/leases", LeaseBody(unitId, tenantId, activate: true)), HttpStatusCode.Created);
        var leaseId = (string)lease["id"]!;

        var run = await Ok(await c.PostAsJsonAsync($"{Re}/billing/run", new { asOf = "2026-11-01", legalEntityId = entityId }));
        Assert.Equal(1, (int)run["invoiced"]!);
        Assert.Equal(30_000m, (decimal)run["totalBilled"]!);
        Assert.Equal(0, (int)(await Ok(await c.PostAsJsonAsync($"{Re}/billing/run", new { asOf = "2026-11-01", legalEntityId = entityId })))["invoiced"]!); // idempotent

        var schedule = (await Ok(await c.GetAsync($"{Re}/leases/{leaseId}/schedule"))).AsArray();
        var first = schedule[0]!;
        Assert.Equal("invoiced", (string)first["status"]!);
        var invoiceId = (string)first["invoiceId"]!;
        var invoice = await Ok(await c.GetAsync($"/api/v1/finance/invoices/{invoiceId}"));
        Assert.Equal("issued", (string)invoice["status"]!);
        Assert.Equal(tenantId, (string)invoice["partyId"]!);
        Assert.StartsWith("INV-", (string)invoice["number"]!);

        // a single instalment can also be invoiced early
        var second = schedule[1]!;
        var early = await Ok(await c.PostAsync($"{Re}/billing/instalments/{(string)second["id"]!}/invoice", null));
        Assert.Equal(30_000m, (decimal)early["total"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsync($"{Re}/billing/instalments/{(string)second["id"]!}/invoice", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/api/v1/finance/invoices/{invoiceId}/void", null)).StatusCode);
        var after = (await Ok(await c.GetAsync($"{Re}/leases/{leaseId}/schedule"))).AsArray()[0]!;
        Assert.Equal("scheduled", (string)after["status"]!);
        Assert.Null(after["invoiceId"]);
    }
}
