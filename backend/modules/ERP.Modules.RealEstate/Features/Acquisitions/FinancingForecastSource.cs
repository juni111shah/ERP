using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Unpaid financing instalments are expected cash outflows on their due dates.</summary>
public sealed class FinancingForecastSource(IReadRepository<FundingInstalment> instalments) : ICashForecastSource
{
    public string Key => "financing";
    public string Label => "Financing";
    public string? ModuleKey => RealEstateModule.Key;

    public async Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? entityId, DateOnly asOf, DateOnly horizon, CancellationToken ct)
    {
        var rows = await instalments.Query(new Specification<FundingInstalment>(i => i.Status == InstalmentPayStatus.Scheduled && i.DueDate <= horizon && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new { i.DueDate, i.Total }).ToListAsync(ct);
        return rows.Select(r => new ForecastItem(r.DueDate, -r.Total)).ToList();
    }
}
