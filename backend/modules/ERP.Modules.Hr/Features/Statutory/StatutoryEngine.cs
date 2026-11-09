using ERP.Application.Abstractions;
using ERP.Modules.Hr.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Hr.Features;

public sealed record StatutoryLineResult(Guid RuleId, string Code, string Name, StatutoryKind Kind, decimal BaseAmount, decimal Amount);

/// <summary>Loads an entity's statutory rules for a month and works out what applies to an employee.</summary>
public sealed class StatutoryEngine(IReadRepository<StatutoryRule> rules)
{
    public async Task<IReadOnlyList<StatutoryRule>> LoadAsync(Guid legalEntityId, int year, int month, CancellationToken ct)
    {
        var start = new DateOnly(year, month, 1); var end = start.AddMonths(1).AddDays(-1);
        var all = await rules.Query(new Specification<StatutoryRule>(r => r.LegalEntityId == legalEntityId && r.IsActive).Include(r => r.Slabs)).ToListAsync(ct);
        return all.Where(r => StatutoryMath.InForce(r, start, end)).OrderBy(r => r.Code).ToList();
    }

    public static IReadOnlyList<StatutoryLineResult> For(IReadOnlyList<StatutoryRule> rules, string? nationality, decimal basic, decimal gross)
        => rules.Where(r => StatutoryMath.AppliesTo(r.Nationalities, nationality))
            .Select(r => { var b = StatutoryMath.BaseFor(r, basic, gross); return new StatutoryLineResult(r.Id, r.Code, r.Name, r.Kind, b, StatutoryMath.Amount(r, b)); })
            .Where(x => x.Amount > 0).ToList();
}
