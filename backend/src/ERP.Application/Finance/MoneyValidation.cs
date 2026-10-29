using FluentValidation;

namespace ERP.Application.Finance;

public static class MoneyValidation
{
    /// <summary>True when the amount has at most two decimal places (cents).</summary>
    public static bool IsCents(decimal value) => decimal.Round(value, 2) == value;

    /// <summary>Money inputs are held and stored to the cent, so a value with more decimals is rejected instead of silently rounded later.</summary>
    public static IRuleBuilderOptions<T, decimal> MustBeMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule.Must(IsCents).WithMessage("Amounts can have at most 2 decimal places.");
}
