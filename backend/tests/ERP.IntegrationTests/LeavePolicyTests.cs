using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>TASK-142: service tiers, pro-rata, monthly accrual, half days, year-end split, supporting document rule.</summary>
public class LeavePolicyTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private static string Code(string p) => p + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private sealed record Ctx(HttpClient C, string Entity);

    private async Task<Ctx> SetupAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        // these tests drive the whole flow with one account; the self-approval block has its own tests in HrAccessTests
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync("/api/v1/settings", new Dictionary<string, object> { ["hr.blockSelfApproval"] = false })).StatusCode);
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Policy " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        return new Ctx(c, entity);
    }

    private static async Task<string> EmployeeAsync(Ctx x, string joinDate)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", new { legalEntityId = x.Entity, fullName = "Policy Tester", designation = "Agent", joinDate, status = "active", basicSalary = 3000m, housingAllowance = 0m, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = 0m }), HttpStatusCode.Created))["id"]!;

    private static async Task<string> TypeAsync(Ctx x, object body)
        => (string)(await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave-types", body), HttpStatusCode.Created))["id"]!;

    private static object Req(string employee, string type, string from, string to, bool submit = false, bool startHalf = false, bool endHalf = false)
        => new { employeeId = employee, leaveTypeId = type, fromDate = from, toDate = to, submit, startHalfDay = startHalf, endHalfDay = endHalf };

    private static async Task<JsonNode> BalanceAsync(Ctx x, string employee, int year, string typeId)
        => (await Ok(await x.C.GetAsync($"{Hr}/leave/balances?year={year}&employeeId={employee}"))).AsArray().Single(r => (string)r!["leaveTypeId"]! == typeId)!;

    [Fact]
    public async Task Entitlement_follows_the_service_tiers_and_pro_rata_rounds_to_half_a_day()
    {
        var x = await SetupAsync();
        var annual = await TypeAsync(x, new { code = Code("TR"), name = "Tiered", isPaid = true, annualDays = 21m, carryForwardCap = 0m, proRataFirstYear = true, tiers = new[] { new { fromServiceYears = 5m, days = 28m } } });
        var two = await EmployeeAsync(x, "2029-01-01");
        var six = await EmployeeAsync(x, "2025-01-01");
        var joiner = await EmployeeAsync(x, "2031-04-15");
        Assert.Equal(21m, (decimal)(await BalanceAsync(x, two, 2031, annual))["entitled"]!);
        Assert.Equal(28m, (decimal)(await BalanceAsync(x, six, 2031, annual))["entitled"]!);
        Assert.Equal(16m, (decimal)(await BalanceAsync(x, joiner, 2031, annual))["entitled"]!); // 21 x 9/12 = 15.75
        Assert.Equal(21m, (decimal)(await BalanceAsync(x, joiner, 2032, annual))["entitled"]!);

        // the tiers come back with the type, and can be replaced
        var types = (await Ok(await x.C.GetAsync($"{Hr}/leave-types?pageSize=100"))).AsArray();
        var shown = types.Single(t => (string)t!["id"]! == annual)!;
        Assert.Equal(28m, (decimal)shown["tiers"]![0]!["days"]!);
        await Ok(await x.C.PutAsJsonAsync($"{Hr}/leave-types/{annual}", new { code = (string)shown["code"]!, name = "Tiered", isPaid = true, annualDays = 21m, carryForwardCap = 0m, proRataFirstYear = true, tiers = new[] { new { fromServiceYears = 1m, days = 24m } } }));
        Assert.Equal(24m, (decimal)(await BalanceAsync(x, six, 2033, annual))["entitled"]!);
    }

    [Fact]
    public async Task Monthly_accrual_limits_what_can_be_booked()
    {
        var x = await SetupAsync();
        var type = await TypeAsync(x, new { code = Code("MO"), name = "Monthly", isPaid = true, annualDays = 12m, carryForwardCap = 0m, accrualMode = "monthly" });
        var e = await EmployeeAsync(x, "2020-01-01");
        // by the end of March three days (3/12 of 12) have accrued: Mon 3 - Thu 6 March 2031 is four
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-03", "2031-03-06", submit: true))).StatusCode);
        var ok = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-03", "2031-03-05", submit: true)), HttpStatusCode.Created);
        Assert.Equal("submitted", (string)ok["status"]!);
    }

    [Fact]
    public async Task Half_days_count_half_and_must_fall_on_working_days()
    {
        var x = await SetupAsync();
        var type = await TypeAsync(x, new { code = Code("HD"), name = "Half", isPaid = true, annualDays = 10m, carryForwardCap = 0m });
        var e = await EmployeeAsync(x, "2020-01-01");
        // Mon 3 - Wed 5 March 2031 with half days at both ends = 0.5 + 1 + 0.5
        Assert.Equal(2.0m, (decimal)(await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-03", "2031-03-05", startHalf: true, endHalf: true)), HttpStatusCode.Created))["days"]!);
        // a single day with a half-day flag is 0.5
        var single = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-10", "2031-03-10", submit: true, startHalf: true)), HttpStatusCode.Created);
        Assert.Equal(0.5m, (decimal)single["days"]!);
        Assert.True((bool)single["startHalfDay"]!);
        // Saturday 8 March 2031
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-08", "2031-03-10", startHalf: true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-14", "2031-03-16", endHalf: true))).StatusCode);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/holidays", new { date = "2031-03-20", name = "Half day holiday" }), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2031-03-20", "2031-03-20", startHalf: true))).StatusCode);
    }

    [Fact]
    public async Task A_request_across_new_year_is_deducted_and_restored_per_year()
    {
        var x = await SetupAsync();
        var type = await TypeAsync(x, new { code = Code("YE"), name = "Year end", isPaid = true, annualDays = 10m, carryForwardCap = 0m });
        var e = await EmployeeAsync(x, "2020-01-01");
        // Mon 27 Dec 2032 - Wed 5 Jan 2033: 5 working days in 2032 and 3 in 2033
        var split = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2032-12-27", "2033-01-05", submit: true)), HttpStatusCode.Created);
        Assert.Equal(8m, (decimal)split["days"]!);
        var id = (string)split["id"]!;
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{id}/approve", new { }));
        Assert.Equal(5m, (decimal)(await BalanceAsync(x, e, 2032, type))["taken"]!);
        Assert.Equal(3m, (decimal)(await BalanceAsync(x, e, 2033, type))["taken"]!);
        await Ok(await x.C.PostAsync($"{Hr}/leave/{id}/cancel", null));
        Assert.Equal(0m, (decimal)(await BalanceAsync(x, e, 2032, type))["taken"]!);
        Assert.Equal(0m, (decimal)(await BalanceAsync(x, e, 2033, type))["taken"]!);

        // use 9 of the 10 days of 2033; the January part of a new year-end request (3 days) no longer fits
        var january = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2033-01-10", "2033-01-20", submit: true)), HttpStatusCode.Created);
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave/{(string)january["id"]!}/approve", new { }));
        var refused = await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, type, "2032-12-27", "2033-01-05", submit: true));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("2033", await refused.Content.ReadAsStringAsync());
    }

    private static MultipartFormDataContent Form(string ownerId, string name)
        => new() { { new StringContent("hr.leaverequest"), "ownerType" }, { new StringContent(ownerId), "ownerId" }, { new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 certificate")) { Headers = { ContentType = new("application/pdf") } }, "file", name } };

    [Fact]
    public async Task A_long_sick_request_needs_a_certificate_before_it_can_be_submitted()
    {
        var x = await SetupAsync();
        var sick = await TypeAsync(x, new { code = Code("SK"), name = "Sick", isPaid = true, annualDays = 0m, carryForwardCap = 0m, requiresAttachmentAfterDays = 2 });
        var e = await EmployeeAsync(x, "2020-01-01");

        // 2 days: no certificate needed
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, sick, "2031-06-02", "2031-06-03", submit: true)), HttpStatusCode.Created);

        // 3 days: refused when submitted straight away and when submitted as a draft without a file
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, sick, "2031-06-09", "2031-06-11", submit: true))).StatusCode);
        var draft = await Ok(await x.C.PostAsJsonAsync($"{Hr}/leave", Req(e, sick, "2031-06-09", "2031-06-11")), HttpStatusCode.Created);
        var id = (string)draft["id"]!;
        var refused = await x.C.PostAsync($"{Hr}/leave/{id}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Attach a medical certificate before submitting.", await refused.Content.ReadAsStringAsync());

        // with a file on the request it goes through
        await Ok(await x.C.PostAsync("/api/v1/attachments", Form(id, "certificate.pdf")), HttpStatusCode.Created);
        Assert.Equal("submitted", (string)(await Ok(await x.C.PostAsync($"{Hr}/leave/{id}/submit", null)))["status"]!);
    }
}

/// <summary>TASK-142: country defaults for a Qatar entity.</summary>
public class LeaveCountryDefaultsQatarTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    [Fact]
    public async Task Qatar_defaults_are_created_once()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Doha " + Guid.NewGuid().ToString("N")[..6], countryCode = "QA", baseCurrency = "QAR", isTaxRegistered = false })).Content.ReadAsStringAsync())!)["id"]!;

        var res = await c.PostAsync($"{Hr}/leave/apply-country-defaults?year=2031&legalEntityId={entity}", null);
        var body = ApiFactory.Unwrap(JsonNode.Parse(await res.Content.ReadAsStringAsync())!);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal(5, body["leaveTypesAdded"]!.AsArray().Count);
        var holidays = body["holidaysAdded"]!.AsArray().Select(h => (string)h!["date"]!).ToList();
        Assert.Contains("2031-12-18", holidays); // National Day
        Assert.Contains("2031-02-11", holidays); // second Tuesday of February 2031
        Assert.Equal(2, holidays.Count);

        var types = ApiFactory.Unwrap(JsonNode.Parse(await (await c.GetAsync($"{Hr}/leave-types?pageSize=100")).Content.ReadAsStringAsync())!).AsArray();
        var annual = types.Single(t => (string)t!["code"]! == "ANNUAL")!;
        Assert.Equal(21m, (decimal)annual["annualDays"]!);
        Assert.Equal(28m, (decimal)annual["tiers"]![0]!["days"]!);
        Assert.Equal(2, (int)types.Single(t => (string)t!["code"]! == "SICK")!["requiresAttachmentAfterDays"]!);
        Assert.Equal(50m, (decimal)types.Single(t => (string)t!["code"]! == "MATERNITY")!["annualDays"]!);

        var again = ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsync($"{Hr}/leave/apply-country-defaults?year=2031&legalEntityId={entity}", null)).Content.ReadAsStringAsync())!);
        Assert.Empty(again["leaveTypesAdded"]!.AsArray());
        Assert.Empty(again["holidaysAdded"]!.AsArray());
    }
}

/// <summary>TASK-142: country defaults for a UAE entity.</summary>
public class LeaveCountryDefaultsUaeTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";

    [Fact]
    public async Task Uae_defaults_are_created_once()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        await first.PostAsync("/api/v1/modules/hr/enable", null);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Dubai " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false })).Content.ReadAsStringAsync())!)["id"]!;

        var body = ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsync($"{Hr}/leave/apply-country-defaults?year=2031&legalEntityId={entity}", null)).Content.ReadAsStringAsync())!);
        Assert.Equal(5, body["leaveTypesAdded"]!.AsArray().Count);
        var holidays = body["holidaysAdded"]!.AsArray().Select(h => (string)h!["date"]!).OrderBy(d => d).ToList();
        Assert.Equal(["2031-01-01", "2031-12-02", "2031-12-03"], holidays);
        var types = ApiFactory.Unwrap(JsonNode.Parse(await (await c.GetAsync($"{Hr}/leave-types?pageSize=100")).Content.ReadAsStringAsync())!).AsArray();
        Assert.Equal(30m, (decimal)types.Single(t => (string)t!["code"]! == "ANNUAL")!["annualDays"]!);

        var again = ApiFactory.Unwrap(JsonNode.Parse(await (await c.PostAsync($"{Hr}/leave/apply-country-defaults?year=2031&legalEntityId={entity}", null)).Content.ReadAsStringAsync())!);
        Assert.Empty(again["leaveTypesAdded"]!.AsArray());
        Assert.Empty(again["holidaysAdded"]!.AsArray());
    }
}
