using FluentValidation;

namespace ERP.Modules.Inventory.Features;

/// <summary>Shared rules for stock commands: a real date, and at most 4 decimals (what the stock ledger keeps) within a sane range.</summary>
internal static class StockRules
{
    public static IRuleBuilderOptions<T, DateOnly> ValidStockDate<T>(this IRuleBuilder<T, DateOnly> rule)
        => rule.Must(d => d != default && d >= DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow).AddYears(-10) && d <= DateOnly.FromDateTime(ERP.Domain.Common.AppClock.UtcNow).AddYears(10))
            .WithMessage("Enter a valid date.");

    public static IRuleBuilderOptions<T, decimal> FourDecimals<T>(this IRuleBuilder<T, decimal> rule)
        => rule.Must(v => decimal.Round(v, 4) == v && Math.Abs(v) < 1_000_000_000m).WithMessage("Use at most 4 decimals and less than 1,000,000,000.");
}
