using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetAssetScheduleHandler(IFixedAssetRepository assets, IDepreciationEntryRepository entries, IDepreciationRunRepository runs) : IQueryHandler<GetAssetScheduleQuery, AssetScheduleDto>
{
    public async Task<AssetScheduleDto> Handle(GetAssetScheduleQuery q, CancellationToken ct)
    {
        // 1. The asset, its charges and the journals of their runs.
        var a = await assets.GetAsync(q.AssetId, ct);
        var posted = await entries.ListForAssetAsync(q.AssetId, ct);
        var runIds = posted.Select(p => p.RunId).ToList();
        var journals = await runs.MapJournalIdsAsync(runIds, ct);
        var lastPosted = posted.Count == 0 ? (DateOnly?)null : posted.Max(p => p.PeriodEnd);

        // 2. The planned schedule (up to the disposal, or what was posted after it), marking each posted month.
        var rows = DepreciationCalculator.Schedule(DepreciationCalculator.PlanOf(a))
            .Where(p => a.DisposedOn is not { } gone || p.PeriodEnd <= gone || (lastPosted is { } l && p.PeriodEnd <= l))
            .Select(p =>
            {
                var entry = posted.FirstOrDefault(e => e.PeriodEnd == p.PeriodEnd);
                return new AssetScheduleRow(p.Year, p.Month, p.PeriodEnd, p.Amount, p.Accumulated, p.NetBookValue, lastPosted is { } last && p.PeriodEnd <= last, entry?.Amount, entry?.RunId, entry is null ? null : journals.GetValueOrDefault(entry.RunId));
            }).ToList();
        return new AssetScheduleDto(a.Id, a.Number, a.Name, a.Cost, a.SalvageValue, a.Method, a.LifeMonths, a.Status, a.AccumulatedDepreciation, rows);
    }
}
