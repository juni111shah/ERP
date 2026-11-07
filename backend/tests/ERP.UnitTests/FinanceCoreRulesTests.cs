using ERP.Application.Finance;
using ERP.Application.Security;
using ERP.Application.Workflow;
using ERP.Domain.Common;
using ERP.Domain.Finance;
using ERP.Domain.Platform;
using ERP.Modularity;
using ERP.Modules.Assets;
using ERP.Modules.Assets.Domain;
using ERP.Modules.Assets.Features;
using Xunit;

namespace ERP.UnitTests;

public class FiscalYearTests
{
    [Fact]
    public void A_calendar_year_is_january_to_december()
    {
        Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), FiscalYears.Range(2026, 1));
        Assert.Equal(new DateOnly(2026, 1, 1), FiscalYears.StartOf(new DateOnly(2026, 11, 6), 1));
    }

    [Fact]
    public void A_fiscal_year_is_named_for_the_year_it_ends_in()
    {
        Assert.Equal((new DateOnly(2025, 4, 1), new DateOnly(2026, 3, 31)), FiscalYears.Range(2026, 4));
        Assert.Equal(new DateOnly(2024, 3, 1), FiscalYears.Range(2025, 3).Start);
        Assert.Equal(new DateOnly(2025, 2, 28), FiscalYears.Range(2025, 3).End);
        Assert.Equal(new DateOnly(2026, 4, 1), FiscalYears.StartOf(new DateOnly(2026, 4, 1), 4));
        Assert.Equal(new DateOnly(2025, 4, 1), FiscalYears.StartOf(new DateOnly(2026, 3, 31), 4));
    }

    [Fact]
    public void A_nonsense_start_month_falls_back_to_january()
        => Assert.Equal((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), FiscalYears.Range(2026, 13));
}

public class CashFlowRuleTests
{
    [Theory]
    [InlineData(AccountType.Equity, "Equity", "equity.retained", CashFlowCategory.Financing)]
    [InlineData(AccountType.Income, "Revenue", "income.sales", CashFlowCategory.Operating)]
    [InlineData(AccountType.Expense, "Operating expenses", null, CashFlowCategory.Operating)]
    [InlineData(AccountType.Asset, "Current assets", null, CashFlowCategory.Operating)]
    [InlineData(AccountType.Asset, "Non-current assets", null, CashFlowCategory.Investing)]
    [InlineData(AccountType.Asset, "Fixed assets", null, CashFlowCategory.Investing)]
    [InlineData(AccountType.Liability, "Current liabilities", null, CashFlowCategory.Operating)]
    [InlineData(AccountType.Liability, "Non-current liabilities", null, CashFlowCategory.Financing)]
    [InlineData(AccountType.Liability, "Loans", null, CashFlowCategory.Financing)]
    [InlineData(AccountType.Liability, "Non-current liabilities", "payroll.gratuity.provision", CashFlowCategory.Operating)]
    [InlineData(AccountType.Asset, null, null, CashFlowCategory.Operating)]
    public void Accounts_are_classified_by_type_group_and_role(AccountType type, string? group, string? role, CashFlowCategory expected)
        => Assert.Equal(expected, CashFlowClassifier.Classify(type, group, role));

    private static readonly Guid Bank = Guid.NewGuid(), Cash = Guid.NewGuid(), A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    private static readonly HashSet<Guid> CashAccounts = [Bank, Cash];

    [Fact]
    public void Money_in_comes_from_the_credits_in_proportion_and_the_parts_add_up_to_the_cent()
    {
        var parts = CashFlowAllocator.Allocate([new(Bank, 100m, 0), new(A, 0, 33.33m), new(B, 0, 33.33m), new(C, 0, 33.34m)], CashAccounts);
        Assert.Equal(3, parts.Count);
        Assert.Equal(100m, parts.Sum(p => p.Amount));
        Assert.All(parts, p => Assert.True(p.Amount > 0));
    }

    [Fact]
    public void Money_out_goes_to_the_debits_and_same_side_lines_moved_no_cash()
    {
        // Sold for 500 with 800 accumulated depreciation and a 200 loss against 1500 cost: only the cost line pairs with the cash.
        var inflow = CashFlowAllocator.Allocate([new(Bank, 500m, 0), new(A, 800m, 0), new(B, 200m, 0), new(C, 0, 1500m)], CashAccounts);
        Assert.Equal((C, 500m), Assert.Single(inflow));
        var outflow = CashFlowAllocator.Allocate([new(A, 70m, 0), new(B, 30m, 0), new(Bank, 0, 100m)], CashAccounts);
        Assert.Equal(-100m, outflow.Sum(p => p.Amount));
        Assert.Equal(-70m, outflow.Single(p => p.AccountId == A).Amount);
    }

    [Fact]
    public void Moving_money_between_own_accounts_is_no_flow_but_a_net_amount_is()
    {
        Assert.Empty(CashFlowAllocator.Allocate([new(Bank, 250m, 0), new(Cash, 0, 250m)], CashAccounts));
        var net = CashFlowAllocator.Allocate([new(Bank, 100m, 0), new(Cash, 0, 30m), new(A, 0, 70m)], CashAccounts);
        Assert.Equal((A, 70m), Assert.Single(net));
    }
}

public class ApprovalChainRuleTests
{
    private static ApprovalPolicy Policy(int level, string permission, decimal? min = null, bool active = true, string document = "journal")
        => new() { Level = level, ApproverPermission = permission, MinAmount = min, IsActive = active, DocumentType = document };

    [Fact]
    public void The_chain_is_ordered_by_level_and_skips_policies_the_amount_does_not_reach()
    {
        var chain = ApprovalRouting.Chain([Policy(2, "b.c.d", 1_000m), Policy(1, "a.b.c"), Policy(3, "x.y.z", 10_000m)], 5_000m);
        Assert.Equal([(1, "a.b.c"), (2, "b.c.d")], chain);
    }

    [Fact]
    public void Rows_of_one_level_are_tiers_and_the_highest_reached_wins()
    {
        var policies = new[] { Policy(1, "small.tier.ok"), Policy(1, "big.tier.ok", 1_000m) };
        Assert.Equal("small.tier.ok", ApprovalRouting.Chain(policies, 999m).Single().Permission);
        Assert.Equal("big.tier.ok", ApprovalRouting.Chain(policies, 1_000m).Single().Permission);
    }

    [Fact]
    public void Without_a_matching_policy_the_core_approver_decides_and_old_rows_read_as_the_core_permission()
    {
        Assert.Equal([(1, CorePermissions.ApprovalsDecide)], ApprovalRouting.Chain([Policy(1, "a.b.c", 100m)], 10m));
        Assert.Equal([(1, CorePermissions.ApprovalsDecide)], ApprovalRouting.Chain([Policy(1, "a.b.c", active: false)], 10m));
        Assert.Equal(CorePermissions.ApprovalsDecide, ApprovalRouting.EffectivePermission("approvals.decide"));
        Assert.Equal("a.b.c", ApprovalRouting.EffectivePermission("a.b.c"));
    }

    private static ApprovalRequest TwoLevels(Guid maker) => new()
    {
        RequestedBy = maker, Subject = "x", DocumentType = "journal",
        Steps = [new ApprovalStep { Level = 1, ApproverPermission = "a.b.c" }, new ApprovalStep { Level = 2, ApproverPermission = "d.e.f" }],
    };

    [Fact]
    public void Approving_a_level_passes_to_the_next_and_the_last_one_approves_the_request()
    {
        var (maker, first, second) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var r = TwoLevels(maker);
        r.Decide(first, true, "one", DateTime.UtcNow);
        Assert.Equal(ApprovalStatus.Pending, r.Status);
        Assert.Null(r.DecidedBy);
        Assert.Equal(2, r.CurrentStep!.Level);

        Assert.Throws<DomainException>(() => r.Decide(first, true, null, DateTime.UtcNow)); // nobody decides two levels
        Assert.Throws<DomainException>(() => r.Decide(maker, true, null, DateTime.UtcNow));
        r.Decide(second, true, "two", DateTime.UtcNow);
        Assert.Equal(ApprovalStatus.Approved, r.Status);
        Assert.Equal(second, r.DecidedBy);
        Assert.Null(r.CurrentStep);
        Assert.Equal(["one", "two"], r.Steps.Select(s => s.Comment));
    }

    [Fact]
    public void A_rejection_or_withdrawal_cancels_the_levels_still_open()
    {
        var r = TwoLevels(Guid.NewGuid());
        r.Decide(Guid.NewGuid(), false, "no", DateTime.UtcNow);
        Assert.Equal(ApprovalStatus.Rejected, r.Status);
        Assert.Equal([ApprovalStepStatus.Rejected, ApprovalStepStatus.Cancelled], r.Steps.Select(s => s.Status));

        var maker = Guid.NewGuid();
        var w = TwoLevels(maker);
        w.Withdraw(maker);
        Assert.All(w.Steps, s => Assert.Equal(ApprovalStepStatus.Cancelled, s.Status));
    }

    [Fact]
    public void A_request_from_before_chains_still_takes_one_decision()
    {
        var r = new ApprovalRequest { RequestedBy = Guid.NewGuid(), Subject = "x", DocumentType = "bill" };
        r.Decide(Guid.NewGuid(), true, null, DateTime.UtcNow);
        Assert.Equal(ApprovalStatus.Approved, r.Status);
    }
}

public class ModuleDependencyTests
{
    private sealed class Fake(string key, params string[] requires) : ModuleBase
    {
        public override ModuleManifest Manifest { get; } = new(key, key, key, "Test", Requires: requires);
    }

    private static readonly Dictionary<string, IModule> Installed = new[]
    {
        new Fake("base"), new Fake("mid", "base"), new Fake("top", "mid"), new Fake("orphan", "missing"), new Fake("loopa", "loopb"), new Fake("loopb", "loopa"), new Fake("alone"),
    }.ToDictionary(m => m.Manifest.Key, m => (IModule)m);

    private static IModule? Find(string key) => Installed.GetValueOrDefault(key);

    [Fact]
    public void A_suggested_module_brings_its_dependencies_first()
    {
        Assert.Equal(["base", "mid", "top"], ModuleDependencies.Resolve(["top"], Find));
        Assert.Equal(["base", "mid", "alone"], ModuleDependencies.Resolve(["mid", "alone", "base", "mid"], Find));
    }

    [Fact]
    public void What_cannot_be_satisfied_stays_off()
    {
        Assert.Empty(ModuleDependencies.Resolve(["orphan"], Find));
        Assert.Empty(ModuleDependencies.Resolve(["notinstalled"], Find));
        Assert.Empty(ModuleDependencies.Resolve(["loopa"], Find));
        Assert.Equal(["alone"], ModuleDependencies.Resolve(["orphan", "alone"], Find));
    }

    [Fact]
    public void A_plan_limit_applies_to_dependencies_too()
    {
        Assert.Empty(ModuleDependencies.Resolve(["top"], Find, ["top", "mid"])); // base is left out, so top cannot run
        Assert.Equal(["base", "mid"], ModuleDependencies.Resolve(["top", "mid"], Find, ["mid", "base"]));
        Assert.Equal(["base", "mid", "top"], ModuleDependencies.Resolve(["top"], Find, []));
    }
}

public class AssetsModuleRuleTests
{
    private static DepreciationPlan Plan(decimal cost, decimal salvage, int life, DepreciationMethod method = DepreciationMethod.StraightLine, decimal? rate = null)
        => new(cost, salvage, life, method, rate, new DateOnly(2025, 1, 15));

    [Fact]
    public void Straight_line_lands_exactly_on_the_salvage_value_with_the_cents_spread()
    {
        var s = DepreciationCalculator.Schedule(Plan(1_000m, 0m, 3));
        Assert.Equal([333.33m, 333.34m, 333.33m], s.Select(p => p.Amount));
        Assert.Equal(1_000m, s[^1].Accumulated);
        Assert.Equal(0m, s[^1].NetBookValue);
        Assert.Equal(new DateOnly(2025, 2, 28), s[0].PeriodEnd); // starts the month after acquisition
        Assert.Equal((2025, 4), (s[^1].Year, s[^1].Month));

        var withSalvage = DepreciationCalculator.Schedule(Plan(1_000m, 100m, 10));
        Assert.Equal(10, withSalvage.Count);
        Assert.All(withSalvage, p => Assert.Equal(90m, p.Amount));
        Assert.Equal(100m, withSalvage[^1].NetBookValue);
    }

    [Fact]
    public void Declining_balance_charges_a_rate_of_what_is_left_and_writes_down_to_salvage_at_the_end_of_life()
    {
        var s = DepreciationCalculator.Schedule(Plan(1_000m, 100m, 10, DepreciationMethod.DecliningBalance));
        Assert.Equal(200m, s[0].Amount);    // 2/10 of 1000
        Assert.Equal(160m, s[1].Amount);    // 2/10 of 800
        Assert.Equal(128m, s[2].Amount);    // 2/10 of 640
        Assert.Equal(900m, s[^1].Accumulated);
        Assert.Equal(100m, s[^1].NetBookValue);
        Assert.Equal(900m, s.Sum(p => p.Amount));
        Assert.All(s, p => Assert.True(p.NetBookValue >= 100m));
    }

    [Fact]
    public void A_declining_rate_given_is_an_annual_rate_and_a_high_salvage_stops_it_early()
    {
        var yearly = DepreciationCalculator.Schedule(Plan(1_200m, 0m, 24, DepreciationMethod.DecliningBalance, 60m));
        Assert.Equal(60m, yearly[0].Amount); // 5% a month
        var stopped = DepreciationCalculator.Schedule(Plan(1_000m, 700m, 10, DepreciationMethod.DecliningBalance));
        Assert.Equal(300m, stopped[^1].Accumulated);
        Assert.True(stopped.Count < 10);
    }

    [Fact]
    public void What_is_due_by_a_date_follows_the_schedule()
    {
        var plan = Plan(1_000m, 100m, 10);
        Assert.Equal(0m, DepreciationCalculator.AccumulatedThrough(plan, new DateOnly(2025, 1, 31)));
        Assert.Equal(0m, DepreciationCalculator.AccumulatedThrough(plan, new DateOnly(2025, 2, 27)));
        Assert.Equal(90m, DepreciationCalculator.AccumulatedThrough(plan, new DateOnly(2025, 2, 28)));
        Assert.Equal(900m, DepreciationCalculator.AccumulatedThrough(plan, new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Nothing_to_depreciate_gives_an_empty_schedule()
    {
        Assert.Empty(DepreciationCalculator.Schedule(Plan(100m, 100m, 10)));
        Assert.Empty(DepreciationCalculator.Schedule(Plan(100m, 0m, 0)));
    }

    [Fact]
    public void The_manifest_registers_keys_menu_and_permissions_of_the_module()
    {
        var module = new AssetsModule();
        Assert.Equal("assets", module.Manifest.Key);
        Assert.All(module.Permissions, p => Assert.Matches("^assets\\.[a-z]+\\.[a-z]+$", p.Key));
        Assert.Equal(Permissions.All.OrderBy(k => k), module.Permissions.Select(p => p.Key).OrderBy(k => k));
        Assert.All(module.Menu, m => Assert.StartsWith("/assets/", m.Route));
        Assert.Contains(module.IndustryTemplates.SelectMany(t => t.Sequences ?? []), s => s.DocumentType == AssetsModule.AssetSequence);
    }
}
