using ERP.Application.Abstractions;
using ERP.Application.Organizations;
using ERP.Modules.Hr.Repositories;

namespace ERP.Modules.Hr.Features;

/// <summary>The end-of-service liability per employee as of a date (the gratuity report and the provision true-up).</summary>
public sealed class GratuityLiability(IEmployeeRepository employees, ILegalEntityRepository legalEntities, GratuityCalculator calculator, ISettingsService settings)
{
    public async Task<(List<GratuityRow> Rows, string? Rule)> BuildAsync(DateOnly asOf, Guid? entityId, CancellationToken ct)
    {
        var staff = await employees.ListNotTerminatedJoinedByAsync(entityId, asOf, ct);
        var countries = (await legalEntities.ListAllowedAsync(null, ct)).ToDictionary(l => l.Id, l => l.CountryCode);
        var daysBasis = new Dictionary<Guid, int>();
        foreach (var id in staff.Select(e => e.LegalEntityId).Distinct()) daysBasis[id] = await settings.GetAsync<int?>("hr.daysBasis", id, ct) ?? 30;
        string? rule = null;
        var rows = staff.Select(e =>
        {
            var g = calculator.Calculate(e.BasicSalary, e.JoinDate, e.TerminationDate, asOf, countries.GetValueOrDefault(e.LegalEntityId, ""), daysBasis[e.LegalEntityId]);
            rule ??= g.Rule;
            return new GratuityRow(e.Id, e.Number, e.FullName, e.LegalEntityId, e.JoinDate, e.BasicSalary, Math.Round(g.ServiceYears, 2), g.Eligible, g.EntitlementDays, g.Amount);
        }).ToList();
        return (rows, rule);
    }
}
