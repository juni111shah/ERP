using ERP.Application.Finance;
using ERP.Modules.Assets.Domain;

namespace ERP.Modules.Assets.Features;

public sealed record DepreciationPlan(decimal Cost, decimal SalvageValue, int LifeMonths, DepreciationMethod Method, decimal? DecliningRatePercent, DateOnly AcquisitionDate);

public sealed record SchedulePeriod(int Year, int Month, DateOnly PeriodEnd, decimal Amount, decimal Accumulated, decimal NetBookValue);

/// <summary>
/// The depreciation schedule of an asset (pure; unit-tested). Depreciation starts in the month after acquisition, whole months only, and never goes below the salvage value.
/// Straight line charges the same amount each month, with cents landing so the life adds up exactly. Declining balance charges a fixed rate of the net book value and
/// writes the rest down to salvage in the last month of the life.
/// </summary>
public static class DepreciationCalculator
{
    public static DateOnly FirstPeriod(DateOnly acquisition) => new DateOnly(acquisition.Year, acquisition.Month, 1).AddMonths(1);

    public static DateOnly EndOfMonth(int year, int month) => new DateOnly(year, month, 1).AddMonths(1).AddDays(-1);

    public static IReadOnlyList<SchedulePeriod> Schedule(DepreciationPlan plan)
    {
        var result = new List<SchedulePeriod>();
        var depreciable = plan.Cost - plan.SalvageValue;
        if (depreciable <= 0 || plan.LifeMonths <= 0) return result;
        // Double declining unless a rate is given. Both are annual rates brought down to a month.
        var monthlyRate = plan.DecliningRatePercent is { } annual and > 0 ? annual / 100m / 12m : 2m / plan.LifeMonths;
        var accumulated = 0m;
        var period = FirstPeriod(plan.AcquisitionDate);
        for (var i = 1; i <= plan.LifeMonths && accumulated < depreciable; i++, period = period.AddMonths(1))
        {
            var target = plan.Method == DepreciationMethod.StraightLine
                ? Money.Round(depreciable * i / plan.LifeMonths)
                : i == plan.LifeMonths ? depreciable : Math.Min(depreciable, accumulated + Money.Round((plan.Cost - accumulated) * monthlyRate));
            var amount = target - accumulated;
            accumulated = target;
            if (amount > 0) result.Add(new SchedulePeriod(period.Year, period.Month, EndOfMonth(period.Year, period.Month), amount, accumulated, plan.Cost - accumulated));
        }
        return result;
    }

    /// <summary>What the schedule says has been charged by the end of a date.</summary>
    public static decimal AccumulatedThrough(DepreciationPlan plan, DateOnly date)
        => Schedule(plan).LastOrDefault(p => p.PeriodEnd <= date)?.Accumulated ?? 0m;

    public static DepreciationPlan PlanOf(FixedAsset a) => new(a.Cost, a.SalvageValue, a.LifeMonths, a.Method, a.DecliningRatePercent, a.AcquisitionDate);
}
