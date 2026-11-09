using ERP.Application.Templates;

namespace ERP.Modules.Hr.Features;

public sealed record GratuityResult(decimal ServiceYears, bool Eligible, decimal EntitlementDays, decimal Amount, string? Rule);

/// <summary>End-of-service benefit under the employing entity's country rule (a country pack concern); zero where the country has none.</summary>
public sealed class GratuityCalculator(CountryPackRegistry packs)
{
    /// <summary>The value of an entitlement day is the basic salary over the entity's days-in-a-pay-month setting (<c>hr.daysBasis</c>).</summary>
    public GratuityResult Calculate(decimal basicSalary, DateOnly joined, DateOnly? left, DateOnly asOf, string countryCode, int daysBasis = 30)
    {
        var end = left is { } l && l < asOf ? l : asOf;
        var years = Math.Max(0m, Math.Round((end.DayNumber - joined.DayNumber) / 365.25m, 4));
        var rule = packs.Find(countryCode)?.EndOfService;
        if (rule is null) return new GratuityResult(years, false, 0, 0, null);
        if (years < rule.MinimumYears) return new GratuityResult(years, false, 0, 0, rule.Description);
        var days = Math.Round(rule.EntitlementDays(years), 2);
        return new GratuityResult(years, true, days, PayrollMath.R(basicSalary / (daysBasis > 0 ? daysBasis : 30) * days), rule.Description);
    }
}
