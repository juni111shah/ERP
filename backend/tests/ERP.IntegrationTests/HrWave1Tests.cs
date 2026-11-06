using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ERP.IntegrationTests;

/// <summary>Staff loans and advances recovered through payroll, and termination with final settlement.</summary>
public class HrWave1Tests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = HrKit.Hr;

    private static async Task<string> BankAsync(HrKit kit)
        => (string)(await HrKit.Ok(await kit.Owner.GetAsync("/api/v1/finance/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;

    private static void PlainPay(JsonObject b) { b["housingAllowance"] = 0m; b["transportAllowance"] = 0m; b["recurringDeductions"] = 0m; }

    private static async Task<JsonNode> RunAsync(HrKit kit, int year, int month, bool post = true)
    {
        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year, month }), HttpStatusCode.Created);
        if (post) await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));
        return run;
    }

    [Fact]
    public async Task A_3000_loan_is_recovered_1000_a_month_and_a_void_gives_the_instalment_back()
    {
        var kit = await HrKit.StartAsync(f);
        var bank = await BankAsync(kit);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Loan"), 3_000m, PlainPay);
        var loan = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "loan", principal = 3_000m, instalments = 3, startYear = 2026, startMonth = 10, reason = "Car" }), HttpStatusCode.Created);
        var id = (string)loan["id"]!;
        Assert.Equal(1_000m, (decimal)loan["instalmentAmount"]!);

        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans/{id}/disburse", new { bankAccountId = bank, date = "2026-10-01" }));
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/loans/{id}/disburse", new { bankAccountId = bank, date = "2026-10-02" })).StatusCode); // only once

        var october = await RunAsync(kit, 2026, 10);
        var line = october["lines"]!.AsArray().Single()!;
        Assert.Equal(1_000m, (decimal)line["loanRecovery"]!);
        Assert.Equal(2_000m, (decimal)line["netPay"]!);
        Assert.Equal(2_000m, (decimal)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans/{id}")))["remaining"]!);

        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll/{(string)october["id"]!}/void", new { reason = "Wrong attendance entered" }));
        var restored = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans/{id}"));
        Assert.Equal(3_000m, (decimal)restored["remaining"]!);
        Assert.Equal("active", (string)restored["status"]!);

        await RunAsync(kit, 2026, 10); await RunAsync(kit, 2026, 11); await RunAsync(kit, 2026, 12);
        var done = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans/{id}"));
        Assert.Equal(0m, (decimal)done["remaining"]!);
        Assert.Equal("settled", (string)done["status"]!);
        var next = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2027, month = 1 }), HttpStatusCode.Created);
        Assert.Equal(0m, (decimal)next["lines"]!.AsArray().Single()!["loanRecovery"]!); // nothing left to recover
    }

    [Fact]
    public async Task A_loan_never_takes_net_pay_below_zero_and_the_rest_waits_for_the_next_month()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Tight"), 1_000m, PlainPay);
        await kit.EmployeeAsync(HrKit.Unique("Steady"), 3_000m, PlainPay); // keeps the run payable when the first employee nets zero
        var loan = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "advance", principal = 5_000m, instalments = 2, instalmentAmount = 2_500m, startYear = 2026, startMonth = 10 }), HttpStatusCode.Created);
        var run = await RunAsync(kit, 2026, 10);
        var line = run["lines"]!.AsArray().Single(l => (string)l!["employeeId"]! == emp)!;
        Assert.Equal(1_000m, (decimal)line["loanRecovery"]!);
        Assert.Equal(0m, (decimal)line["netPay"]!);
        Assert.Equal(4_000m, (decimal)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans/{(string)loan["id"]!}")))["remaining"]!);
    }

    [Fact]
    public async Task A_cancelled_loan_is_not_recovered_and_a_leaver_cannot_get_a_loan()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Cancel"), 3_000m, PlainPay);
        var loan = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "loan", principal = 600m, instalments = 2, startYear = 2026, startMonth = 10 }), HttpStatusCode.Created);
        await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/loans/{(string)loan["id"]!}/cancel", null));
        var run = await RunAsync(kit, 2026, 10, post: false);
        Assert.Equal(0m, (decimal)run["lines"]!.AsArray().Single()!["loanRecovery"]!);

        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-15", reason = "Resigned" }));
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "loan", principal = 100m, instalments = 1, startYear = 2026, startMonth = 12 })).StatusCode);
    }

    [Fact]
    public async Task Terminating_mid_month_gives_a_settlement_with_salary_leave_end_of_service_and_the_loan_balance()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Leaver"), 3_000m, PlainPay);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/leave-types", new { code = HrKit.Unique("EN"), name = "Annual (payable)", isPaid = true, annualDays = 30m, carryForwardCap = 0m, requiresApproval = true, encashable = true }), HttpStatusCode.Created);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "loan", principal = 600m, instalments = 1, startYear = 2027, startMonth = 1 }), HttpStatusCode.Created);

        var preview = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-15"));
        Assert.Equal(1_500m, (decimal)preview["finalBasic"]!);           // 15 of 30 days
        Assert.Equal(3_000m, (decimal)preview["leaveEncashment"]!);      // 30 days x 3000/30
        Assert.True((decimal)preview["endOfService"]! > 0m);
        Assert.Equal(600m, (decimal)preview["loanRecovery"]!);
        var gross = (decimal)preview["finalBasic"]! + (decimal)preview["leaveEncashment"]! + (decimal)preview["endOfService"]!;
        Assert.Equal(gross, (decimal)preview["gross"]!);
        Assert.Equal(gross - 600m, (decimal)preview["netPay"]!);

        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsync($"{Hr}/employees/{emp}/final-settlement", null)).StatusCode); // not terminated yet
        var done = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-15", reason = "Resigned" }));
        Assert.Equal((decimal)preview["netPay"]!, (decimal)done["netPay"]!);
        Assert.Equal("terminated", (string)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}")))["status"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-20" })).StatusCode);

        var run = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/employees/{emp}/final-settlement", null), HttpStatusCode.Created);
        Assert.Equal("finalSettlement", (string)run["kind"]!);
        var line = run["lines"]!.AsArray().Single()!;
        Assert.Equal((decimal)preview["netPay"]!, (decimal)line["netPay"]!);
        Assert.Equal((decimal)preview["endOfService"]!, (decimal)line["endOfService"]!);
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsync($"{Hr}/employees/{emp}/final-settlement", null)).StatusCode); // one per leaver

        // Recalculating would rebuild it as a normal run: every employee added, the settlement amounts lost.
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/recalculate", null)).StatusCode);
        var unchanged = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/payroll/{(string)run["id"]!}"));
        Assert.Single(unchanged["lines"]!.AsArray());

        var posted =await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));
        Assert.Equal("posted", (string)posted["status"]!);
        var loans = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans?pageSize=50"));
        Assert.Equal("settled", (string)loans.AsArray().Single(l => (string)l!["employeeId"]! == emp)!["status"]!);

        // the leaver is not on the regular run of the month he was settled in
        Assert.Equal(HttpStatusCode.Conflict, (await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 11 })).StatusCode);
    }

    [Fact]
    public async Task Notice_not_served_is_deducted_only_when_the_setting_is_on()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Notice"), 3_000m, b => { PlainPay(b); b["noticePeriodDays"] = 30; });
        var off = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-30&noticeGivenOn=2026-11-20"));
        Assert.Equal(0m, (decimal)off["noticeShortfallDeduction"]!);
        Assert.Equal(20m, (decimal)off["noticeShortfallDays"]!);

        await kit.SetAsync("hr.noticeShortfallDeduction", true);
        var on = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-30&noticeGivenOn=2026-11-20"));
        Assert.Equal(2_000m, (decimal)on["noticeShortfallDeduction"]!);  // 20 days x 3000/30
    }
}
