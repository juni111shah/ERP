using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>The arithmetic of one rule; no I/O.</summary>
public static class StatutoryMath
{
    public static decimal BaseFor(StatutoryRule r, decimal basic, decimal gross)
    {
        var b = r.Base == StatutoryBase.Basic ? basic : gross;
        if (r.Cap > 0 && b > r.Cap) b = r.Cap;
        return b < 0 ? 0 : b;
    }

    public static decimal Amount(StatutoryRule r, decimal baseAmount)
    {
        if (baseAmount <= 0 || (r.Threshold > 0 && baseAmount < r.Threshold)) return 0m;
        if (r.Method == StatutoryMethod.Flat) return PayrollMath.R(baseAmount * r.Rate / 100m);
        var slabs = r.Slabs.OrderBy(s => s.FromAmount).ToList();
        decimal total = 0;
        for (var i = 0; i < slabs.Count; i++)
        {
            var upper = i + 1 < slabs.Count ? slabs[i + 1].FromAmount : decimal.MaxValue;
            var part = Math.Min(baseAmount, upper) - slabs[i].FromAmount;
            if (part > 0) total += part * slabs[i].Rate / 100m;
        }
        return PayrollMath.R(total);
    }

    /// <summary>Does the rule apply to this nationality? Blank = everybody; "!Qatari" = everybody but Qataris; "Qatari, Omani" = only them.</summary>
    public static bool AppliesTo(string? nationalities, string? nationality)
    {
        if (string.IsNullOrWhiteSpace(nationalities)) return true;
        var parts = nationalities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var excluded = parts.Where(p => p.StartsWith('!')).Select(p => p[1..].Trim()).ToList();
        var included = parts.Where(p => !p.StartsWith('!')).ToList();
        var n = (nationality ?? "").Trim();
        if (excluded.Any(e => e.Equals(n, StringComparison.OrdinalIgnoreCase))) return false;
        return included.Count == 0 || included.Any(i => i.Equals(n, StringComparison.OrdinalIgnoreCase));
    }

    public static bool InForce(StatutoryRule r, DateOnly monthStart, DateOnly monthEnd)
        => r.IsActive && (r.EffectiveFrom is null || r.EffectiveFrom <= monthEnd) && (r.EffectiveTo is null || r.EffectiveTo >= monthStart);
}
