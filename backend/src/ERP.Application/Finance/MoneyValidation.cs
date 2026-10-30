using FluentValidation;

namespace ERP.Application.Finance;

public static class MoneyValidation
{
    /// <summary>The earliest document date accepted; anything before it is a default or typo, not a real posting period.</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    /// <summary>True when the amount has at most two decimal places (cents).</summary>
    public static bool IsCents(decimal value) => decimal.Round(value, 2) == value;

    /// <summary>True when the value has at most <paramref name="places"/> decimal places.</summary>
    public static bool HasAtMostDecimals(decimal value, int places) => decimal.Round(value, places) == value;

    /// <summary>True for a date after 2000-01-01 and at most one year after <paramref name="today"/>.</summary>
    public static bool IsPlausibleDate(DateOnly value, DateOnly today) => value > EarliestDate && value <= today.AddYears(1);

    /// <summary>Money inputs are held and stored to the cent, so a value with more decimals is rejected instead of silently rounded later.</summary>
    public static IRuleBuilderOptions<T, decimal> MustBeMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule.Must(IsCents).WithMessage("Amounts can have at most 2 decimal places.");

    /// <summary>Quantities and unit prices may carry up to <paramref name="places"/> decimals; more would drift from the rounded line total.</summary>
    public static IRuleBuilderOptions<T, decimal> MustHaveAtMostDecimals<T>(this IRuleBuilder<T, decimal> rule, int places)
        => rule.Must(v => HasAtMostDecimals(v, places)).WithMessage($"This value can have at most {places} decimal places.");

    /// <summary>Document dates must fall after 2000-01-01 and no more than one year ahead (a missing date binds to 0001-01-01).</summary>
    public static IRuleBuilderOptions<T, DateOnly> MustBePlausibleDate<T>(this IRuleBuilder<T, DateOnly> rule)
        => rule.Must(d => IsPlausibleDate(d, DateOnly.FromDateTime(DateTime.UtcNow))).WithMessage("Enter a valid date (after 2000, at most one year ahead).");

    public static IRuleBuilderOptions<T, DateOnly?> MustBePlausibleDate<T>(this IRuleBuilder<T, DateOnly?> rule)
        => rule.Must(d => d is null || IsPlausibleDate(d.Value, DateOnly.FromDateTime(DateTime.UtcNow))).WithMessage("Enter a valid date (after 2000, at most one year ahead).");
}
