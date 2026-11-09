using System.Linq.Expressions;
using ERP.Modules.Hr.Domain;

namespace ERP.Modules.Hr.Features;

public sealed record StatutorySlabInput(decimal FromAmount, decimal Rate);
public sealed record StatutoryRuleInput(Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate = 0, decimal Threshold = 0, decimal Cap = 0,
    string? Nationalities = null, DateOnly? EffectiveFrom = null, DateOnly? EffectiveTo = null, bool IsActive = true, IReadOnlyList<StatutorySlabInput>? Slabs = null);
public sealed record StatutoryRuleDto(Guid Id, Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate, decimal Threshold, decimal Cap,
    string? Nationalities, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool IsActive, IReadOnlyList<StatutorySlabInput> Slabs)
{
    public static StatutoryRuleDto From(StatutoryRule r) => new(r.Id, r.LegalEntityId, r.Code, r.Name, r.Kind, r.Method, r.Base, r.Rate, r.Threshold, r.Cap, r.Nationalities, r.EffectiveFrom, r.EffectiveTo, r.IsActive,
        r.Slabs.OrderBy(s => s.FromAmount).Select(s => new StatutorySlabInput(s.FromAmount, s.Rate)).ToList());
}
public sealed record StatutoryRuleRow(Guid Id, Guid LegalEntityId, string Code, string Name, StatutoryKind Kind, StatutoryMethod Method, StatutoryBase Base, decimal Rate, bool IsActive)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<StatutoryRule, StatutoryRuleRow>> Projection = r => new StatutoryRuleRow(r.Id, r.LegalEntityId, r.Code, r.Name, r.Kind, r.Method, r.Base, r.Rate, r.IsActive);
}
public sealed record StatutoryTotalDto(string Code, string Name, StatutoryKind Kind, int Employees, decimal BaseAmount, decimal Amount);
