using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ERP.Infrastructure.Persistence;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.IntegrationTests;

/// <summary>Payroll journals built from the lines, statutory deductions on a final settlement, the days basis on gratuity and loan recovery only of money paid out.</summary>
public class HrLedgerRulesTests(ApiFactory f) : IClassFixture<ApiFactory>
{
    private const string Hr = HrKit.Hr;
    private const string Fin = "/api/v1/finance";

    private static void PlainPay(JsonObject b) { b["housingAllowance"] = 0m; b["transportAllowance"] = 0m; b["recurringDeductions"] = 0m; }

    private static async Task<string> BankAsync(HrKit kit)
        => (string)(await HrKit.Ok(await kit.Owner.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray().First(a => (string?)a!["systemRole"] == "bank")!["id"]!;

    /// <summary>Debit less credit per system role on one journal.</summary>
    private static async Task<Dictionary<string, decimal>> JournalAsync(HrKit kit, string journalId)
    {
        var roles = (await HrKit.Ok(await kit.Owner.GetAsync($"{Fin}/accounts?pageSize=300"))).AsArray().ToDictionary(a => (string)a!["id"]!, a => (string?)a!["systemRole"] ?? (string)a!["code"]!);
        var journal = await HrKit.Ok(await kit.Owner.GetAsync($"{Fin}/journals/{journalId}"));
        return journal["lines"]!.AsArray().GroupBy(l => roles[(string)l!["accountId"]!]).ToDictionary(g => g.Key, g => g.Sum(l => (decimal)l!["debit"]! - (decimal)l!["credit"]!));
    }

    private static async Task StatutoryRuleAsync(HrKit kit, decimal rate, string kind = "employeeDeduction")
        => await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/statutory-rules", new { legalEntityId = kit.Entity, code = HrKit.Unique("S"), name = "Social insurance", kind, method = "flat", @base = "basic", rate }), HttpStatusCode.Created);

    [Fact]
    public async Task A_stale_total_on_the_run_no_longer_unbalances_the_payroll_journal()
    {
        var kit = await HrKit.StartAsync(f);
        await kit.EmployeeAsync(HrKit.Unique("Stale"), 3_000m, PlainPay);
        var run = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        var id = Guid.Parse((string)run["id"]!);

        // what the seeded demo draft has: a header that disagrees with its lines by 2,000
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<PayrollRun>().IgnoreQueryFilters().Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.NetPay, 1_000m).SetProperty(r => r.GrossEarnings, 1_000m));
        }

        var posted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{id}/post", null));
        Assert.Equal(3_000m, (decimal)posted["netPay"]!);
        var ledger = await JournalAsync(kit, (string)posted["accrualJournalId"]!);
        Assert.Equal(3_000m, ledger["payroll.expense"]);
        Assert.Equal(-3_000m, ledger["payroll.accrued"]);
        Assert.Equal(0m, ledger.Values.Sum());
    }

    [Fact]
    public async Task Adjusting_a_final_settlement_keeps_its_leave_and_end_of_service_and_the_journal_balances()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Adj"), 3_000m, PlainPay);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-15", reason = "Resigned" }));
        var run = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/employees/{emp}/final-settlement", null), HttpStatusCode.Created);
        var line = run["lines"]!.AsArray().Single()!;
        var eos = (decimal)line["endOfService"]!;
        Assert.True(eos > 3_000m);

        // a deduction as large as the salary: without the terminal pay the line would go below its own end of service
        var adjusted = await HrKit.Ok(await kit.Owner.PutAsJsonAsync($"{Hr}/payroll/{(string)run["id"]!}/lines/{(string)line["id"]!}", new { adjustment = -1_000m, note = "Damage" }));
        Assert.Equal((decimal)line["grossEarnings"]! - 1_000m, (decimal)adjusted["lines"]![0]!["grossEarnings"]!);

        var posted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));
        var ledger = await JournalAsync(kit, (string)posted["accrualJournalId"]!);
        Assert.Equal(0m, ledger.Values.Sum());
        Assert.Equal(-(decimal)adjusted["lines"]![0]!["netPay"]!, ledger["payroll.accrued"]);
    }

    [Fact]
    public async Task A_final_settlement_applies_statutory_rules_to_the_final_salary_and_posts_them()
    {
        var kit = await HrKit.StartAsync(f);
        await StatutoryRuleAsync(kit, 10m);
        await StatutoryRuleAsync(kit, 5m, "employerCost");
        var emp = await kit.EmployeeAsync(HrKit.Unique("Stat"), 3_000m, PlainPay);

        var preview = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-15"));
        Assert.Equal(1_500m, (decimal)preview["finalBasic"]!);
        Assert.Equal(150m, (decimal)preview["statutoryDeduction"]!); // 10% of the 1,500 paid, not of the end of service
        Assert.Equal(75m, (decimal)preview["employerCost"]!);
        Assert.Equal((decimal)preview["gross"]! - 150m, (decimal)preview["netPay"]!);

        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/employees/{emp}/terminate", new { terminationDate = "2026-11-15", reason = "Resigned" }));
        var run = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/employees/{emp}/final-settlement", null), HttpStatusCode.Created);
        var line = run["lines"]!.AsArray().Single()!;
        Assert.Equal(150m, (decimal)line["statutoryDeduction"]!);
        Assert.Equal(75m, (decimal)line["employerCost"]!);

        // the amounts reach the filing report and the ledger
        var filing = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/payroll/{(string)run["id"]!}/statutory"))).AsArray();
        Assert.Equal(225m, filing.Sum(r => (decimal)r!["amount"]!));
        var posted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)run["id"]!}/post", null));
        var ledger = await JournalAsync(kit, (string)posted["accrualJournalId"]!);
        Assert.Equal(-(150m + 75m), ledger["payroll.deductions"]);
        Assert.Equal(0m, ledger.Values.Sum());
    }

    [Fact]
    public async Task No_statutory_deduction_when_a_regular_run_already_paid_the_final_month()
    {
        var kit = await HrKit.StartAsync(f);
        await StatutoryRuleAsync(kit, 10m);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Paid"), 3_000m, PlainPay);
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 11 }), HttpStatusCode.Created);
        var preview = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-15"));
        Assert.True((bool)preview["salaryAlreadyPaid"]!);
        Assert.Equal(0m, (decimal)preview["statutoryDeduction"]!);
    }

    [Fact]
    public async Task Gratuity_follows_the_days_basis_setting_instead_of_a_fixed_thirty()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("Basis"), 3_000m, PlainPay);
        var at30 = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-30"));
        var e30 = (decimal)at30["endOfService"]!;
        Assert.True(e30 > 0m);
        await kit.SetAsync("hr.daysBasis", 26);
        try
        {
            var at26 = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-30"));
            Assert.Equal(e30 * 30m / 26m, (decimal)at26["endOfService"]!, 1); // same entitlement days, each worth basic / 26
            var report = (await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/reports/gratuity?asOf=2026-11-30&legalEntityId={kit.Entity}")))["rows"]!.AsArray().Single(r => (string)r!["employeeId"]! == emp)!;
            Assert.Equal((decimal)at26["endOfService"]!, (decimal)report["gratuity"]!);
        }
        finally { await kit.SetAsync("hr.daysBasis", 30); }
    }

    [Fact]
    public async Task A_loan_that_was_never_paid_out_is_not_recovered()
    {
        var kit = await HrKit.StartAsync(f);
        var emp = await kit.EmployeeAsync(HrKit.Unique("NoCash"), 3_000m, PlainPay);
        var loan = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans", new { employeeId = emp, kind = "loan", principal = 600m, instalments = 2, startYear = 2026, startMonth = 10 }), HttpStatusCode.Created);
        var loanId = (string)loan["id"]!;

        var october = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 10 }), HttpStatusCode.Created);
        Assert.Equal(0m, (decimal)october["lines"]!.AsArray().Single()!["loanRecovery"]!);
        var posted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)october["id"]!}/post", null));
        Assert.False((await JournalAsync(kit, (string)posted["accrualJournalId"]!)).ContainsKey("payroll.advances")); // no credit for money never advanced
        Assert.Equal(600m, (decimal)(await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/loans/{loanId}")))["remaining"]!);

        var preview = await HrKit.Ok(await kit.Owner.GetAsync($"{Hr}/employees/{emp}/settlement-preview?terminationDate=2026-11-15"));
        Assert.Equal(0m, (decimal)preview["loanRecovery"]!);
        Assert.Contains(preview["notes"]!.AsArray(), n => ((string)n!).Contains("never paid out"));

        // once it is paid out the next run recovers it, and the journal credits the advances account
        await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/loans/{loanId}/disburse", new { bankAccountId = await BankAsync(kit), date = "2026-10-20" }));
        var november = await HrKit.Ok(await kit.Owner.PostAsJsonAsync($"{Hr}/payroll", new { legalEntityId = kit.Entity, year = 2026, month = 11 }), HttpStatusCode.Created);
        Assert.Equal(300m, (decimal)november["lines"]!.AsArray().Single()!["loanRecovery"]!);
        var novPosted = await HrKit.Ok(await kit.Owner.PostAsync($"{Hr}/payroll/{(string)november["id"]!}/post", null));
        Assert.Equal(-300m, (await JournalAsync(kit, (string)novPosted["accrualJournalId"]!))["payroll.advances"]);
    }
}
