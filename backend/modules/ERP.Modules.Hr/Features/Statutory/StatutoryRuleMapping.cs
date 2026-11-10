using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

/// <summary>Copies a statutory rule input onto a rule and adds its slabs (create and update).</summary>
internal static class StatutoryRuleMapping
{
    public static void Fill(StatutoryRule r, StatutoryRuleInput i, string code)
    {
        r.Code = code; r.Name = i.Name.Trim(); r.Kind = i.Kind; r.Method = i.Method; r.Base = i.Base; r.Rate = i.Method == StatutoryMethod.Flat ? i.Rate : 0m;
        r.Threshold = i.Threshold; r.Cap = i.Cap; r.Nationalities = string.IsNullOrWhiteSpace(i.Nationalities) ? null : i.Nationalities.Trim();
        r.EffectiveFrom = i.EffectiveFrom; r.EffectiveTo = i.EffectiveTo; r.IsActive = i.IsActive;
    }

    public static void AddSlabs(IRepository<StatutorySlab> slabs, StatutoryRule rule, StatutoryRuleInput i)
    {
        if (i.Method != StatutoryMethod.Slab) return;
        foreach (var s in i.Slabs ?? []) { var slab = new StatutorySlab { RuleId = rule.Id, FromAmount = s.FromAmount, Rate = s.Rate }; slabs.Add(slab); rule.Slabs.Add(slab); }
    }
}
