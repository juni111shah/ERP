using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>TASK-140: proration, void, partial payment, off-cycle runs and the deductions account.</summary>
public class PayrollCoreTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = "/api/v1/m/hr";
    private const string Fin = "/api/v1/finance";

    private static async Task<JsonNode> Ok(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Expected {expected} but got {res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? new JsonObject() : ApiFactory.Unwrap(JsonNode.Parse(text)!);
    }

    private sealed record Ctx(HttpClient C, string Entity, string Bank);

    private async Task<Ctx> SetupAsync()
    {
        var (first, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        Assert.True((await first.PostAsync("/api/v1/modules/hr/enable", null)).IsSuccessStatusCode);
        var (c, _) = await f.SignInAsync(ApiFactory.OwnerEmail, workspaceCode: "demo-re");
        var entity = (string)(await Ok(await c.PostAsJsonAsync("/api/v1/legal-entities", new { name = "Core " + Guid.NewGuid().ToString("N")[..6], countryCode = "AE", baseCurrency = "AED", isTaxRegistered = false }), HttpStatusCode.Created))["id"]!;
        var bank = (string)(await Ok(await c.GetAsync($"{Fin}/accounts?pageSize=200"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;
        return new Ctx(c, entity, bank);
    }

    private static async Task<JsonNode> HireAsync(Ctx x, string name, decimal basic, decimal housing = 0m, decimal deductions = 0m, string join = "2020-01-01", string? leave = null)
        => await Ok(await x.C.PostAsJsonAsync($"{Hr}/employees", new
        {
            legalEntityId = x.Entity, fullName = name, designation = "Agent", department = "Ops", joinDate = join, terminationDate = leave, status = leave is null ? "active" : "terminated",
            basicSalary = basic, housingAllowance = housing, transportAllowance = 0m, otherAllowances = 0m, recurringDeductions = deductions, iban = "AE070331234567890123456",
        }), HttpStatusCode.Created);

    private static async Task<decimal> ClosingAsync(Ctx x, string code)
    {
        var tb = await Ok(await x.C.GetAsync($"{Fin}/reports/trial-balance?to=2030-12-31&legalEntityId={x.Entity}"));
        return (decimal?)tb["rows"]!.AsArray().SingleOrDefault(r => (string?)r!["code"] == code)?["closing"] ?? 0m;
    }

    private async Task SetDeductionsRoleAsync(string? role)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ERP.Infrastructure.Persistence.AppDbContext>();
        var codeOfRole = "2310";
        await db.Set<ERP.Domain.Finance.Account>().IgnoreQueryFilters().Where(a => a.Code == codeOfRole).ExecuteUpdateAsync(u => u.SetProperty(a => a.SystemRole, role));
    }

    private static Task<HttpResponseMessage> Generate(Ctx x, int month)
        => x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month });

    private static JsonNode Line(JsonNode run, string name) => run["lines"]!.AsArray().Single(l => (string)l!["employeeName"]! == name)!;

    [Fact]
    public async Task Joiners_and_leavers_are_prorated_by_calendar_days()
    {
        var x = await SetupAsync();
        await HireAsync(x, "Full Month", 3_100m, 620m);
        await HireAsync(x, "New Joiner", 3_100m, 620m, join: "2026-10-21");
        await HireAsync(x, "Early Leaver", 3_100m, 620m, leave: "2026-10-10");
        var run = await Ok(await Generate(x, 10), HttpStatusCode.Created);
        var full = Line(run, "Full Month"); var joiner = Line(run, "New Joiner"); var leaver = Line(run, "Early Leaver");
        Assert.Equal((3_100m, 620m, 31), ((decimal)full["basic"]!, (decimal)full["allowances"]!, (int)full["proratedDays"]!));
        Assert.Equal((1_100m, 220m, 11), ((decimal)joiner["basic"]!, (decimal)joiner["allowances"]!, (int)joiner["proratedDays"]!));
        Assert.Equal((1_000m, 200m, 10), ((decimal)leaver["basic"]!, (decimal)leaver["allowances"]!, (int)leaver["proratedDays"]!));
        Assert.Equal(1_320m, (decimal)joiner["netPay"]!);

        var adjusted = await Ok(await x.C.PutAsJsonAsync($"{Hr}/payroll/{(string)run["id"]!}/lines/{(string)joiner["id"]!}", new { adjustment = 100m }));
        Assert.Equal(1_420m, (decimal)Line(adjusted, "New Joiner")["netPay"]!); // the prorated parts are kept
    }

    [Fact]
    public async Task Voiding_a_posted_run_reverses_the_accrual_and_frees_the_month()
    {
        var x = await SetupAsync();
        await HireAsync(x, "Amir", 6_000m, 2_000m, 500m);
        var id = (string)(await Ok(await Generate(x, 10), HttpStatusCode.Created))["id"]!;
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { reason = "Too early" })).StatusCode); // a draft is deleted, not voided
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
        Assert.Equal(8_000m, await ClosingAsync(x, "6000"));

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { reason = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Generate(x, 10)).StatusCode);
        var voided = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { reason = "Wrong salaries" }));
        Assert.Equal("voided", (string)voided["status"]!);
        Assert.Equal("Wrong salaries", (string)voided["voidReason"]!);
        foreach (var code in new[] { "6000", "2300", "2310", "1250" }) Assert.Equal(0m, await ClosingAsync(x, code));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { reason = "Again" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-29" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.GetAsync($"{Hr}/payroll/{id}/bank-file")).StatusCode);

        var again = await Ok(await Generate(x, 10), HttpStatusCode.Created); // a new Regular run for the month
        Assert.Equal("draft", (string)again["status"]!);
    }

    [Fact]
    public async Task Voiding_a_paid_run_reverses_the_payments_then_the_accrual()
    {
        var x = await SetupAsync();
        await HireAsync(x, "Amir", 6_000m, 0m, 100m);
        await HireAsync(x, "Neha", 4_000m);
        var run = await Ok(await Generate(x, 10), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-28", employeeIds = new[] { (string)Line(run, "Amir")["employeeId"]! } }));
        await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-29" }));
        Assert.Equal(-9_900m, await ClosingAsync(x, "1010"));

        await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/void", new { reason = "Duplicate run" }));
        foreach (var code in new[] { "1010", "2300", "6000", "2310" }) Assert.Equal(0m, await ClosingAsync(x, code));
    }

    [Fact]
    public async Task Paying_part_of_a_run_is_partial_until_every_line_is_paid()
    {
        var x = await SetupAsync();
        var a = await HireAsync(x, "A", 1_000m); var b = await HireAsync(x, "B", 2_000m); await HireAsync(x, "C", 3_000m);
        var run = await Ok(await Generate(x, 10), HttpStatusCode.Created);
        var id = (string)run["id"]!;
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null));

        var part = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-29", employeeIds = new[] { (string)a["id"]!, (string)b["id"]! } }));
        Assert.Equal("partiallyPaid", (string)part["status"]!);
        Assert.Equal(-3_000m, await ClosingAsync(x, "1010"));
        Assert.Equal(-3_000m, await ClosingAsync(x, "2300")); // 6,000 accrued, 3,000 paid
        Assert.NotNull(Line(part, "A")["paidOn"]);
        Assert.Null(Line(part, "C")["paidOn"]);

        var bankFile = await (await x.C.GetAsync($"{Hr}/payroll/{id}/bank-file?unpaidOnly=true")).Content.ReadAsStringAsync();
        Assert.Contains("\"C\"", bankFile); Assert.DoesNotContain("\"A\"", bankFile); Assert.DoesNotContain("\"B\"", bankFile);
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-30", employeeIds = new[] { (string)a["id"]! } })).StatusCode); // already paid

        var rest = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-30" }));
        Assert.Equal("paid", (string)rest["status"]!);
        Assert.Equal(-6_000m, await ClosingAsync(x, "1010"));
        Assert.Equal(0m, await ClosingAsync(x, "2300"));
        Assert.Equal(HttpStatusCode.Conflict, (await x.C.PostAsJsonAsync($"{Hr}/payroll/{id}/pay", new { bankAccountId = x.Bank, date = "2026-10-31" })).StatusCode);
    }

    [Fact]
    public async Task An_off_cycle_run_sits_next_to_the_regular_run_of_the_month()
    {
        var x = await SetupAsync();
        var amir = await HireAsync(x, "Amir", 5_000m);
        await HireAsync(x, "Neha", 4_000m);
        await Ok(await Generate(x, 10), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, (await Generate(x, 10)).StatusCode); // a second Regular run is still refused

        Assert.Equal(HttpStatusCode.BadRequest, (await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10, kind = "offCycle" })).StatusCode); // needs a note
        var off = await Ok(await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10, kind = "offCycle", notes = "Eid bonus", employeeIds = new[] { (string)amir["id"]! } }), HttpStatusCode.Created);
        Assert.Equal("offCycle", (string)off["kind"]!);
        Assert.Equal(1, (int)off["employees"]!);
        Assert.Equal(0m, (decimal)off["netPay"]!);
        var id = (string)off["id"]!;
        var adjusted = await Ok(await x.C.PutAsJsonAsync($"{Hr}/payroll/{id}/lines/{(string)Line(off, "Amir")["id"]!}", new { adjustment = 750m, note = "Bonus" }));
        Assert.Equal(750m, (decimal)adjusted["netPay"]!);
        Assert.Equal(750m, (decimal)(await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/recalculate", null)))["netPay"]!);
        Assert.Equal("posted", (string)(await Ok(await x.C.PostAsync($"{Hr}/payroll/{id}/post", null)))["status"]!);
        Assert.Equal(750m, await ClosingAsync(x, "6000"));
        Assert.Equal(HttpStatusCode.Created, (await x.C.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = x.Entity, year = 2026, month = 10, kind = "offCycle", notes = "Second" })).StatusCode); // off-cycle runs are not limited
    }

    [Fact]
    public async Task Recurring_deductions_credit_the_deductions_account_when_the_workspace_has_one_and_advances_otherwise()
    {
        var x = await SetupAsync();
        await HireAsync(x, "Amir", 6_000m, 0m, 500m);
        var with = (string)(await Ok(await Generate(x, 10), HttpStatusCode.Created))["id"]!;
        await Ok(await x.C.PostAsync($"{Hr}/payroll/{with}/post", null));
        Assert.Equal(-500m, await ClosingAsync(x, "2310"));
        Assert.Equal(0m, await ClosingAsync(x, "1250"));

        // a workspace whose chart predates the account: take the role off every copy for the second run, then restore it
        await SetDeductionsRoleAsync(null);
        try
        {
            await Ok(await Generate(x, 11), HttpStatusCode.Created);
            var without = (string)(await Ok(await x.C.GetAsync($"{Hr}/payroll?legalEntityId={x.Entity}&pageSize=50"))).AsArray().Single(r => (int)r!["month"]! == 11)!["id"]!;
            await Ok(await x.C.PostAsync($"{Hr}/payroll/{without}/post", null));
            Assert.Equal(-500m, await ClosingAsync(x, "1250")); // no deductions account: the old behaviour
            Assert.Equal(-500m, await ClosingAsync(x, "2310"));
        }
        finally { await SetDeductionsRoleAsync("payroll.deductions"); }
    }
}
