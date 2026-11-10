using ERP.Application.Common;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetDepreciationRunHandler(IDepreciationRunRepository runs, IDepreciationEntryRepository entryRows, IFixedAssetRepository assets) : IQueryHandler<GetDepreciationRunQuery, DepreciationRunDto>
{
    public async Task<DepreciationRunDto> Handle(GetDepreciationRunQuery q, CancellationToken ct)
    {
        // 1. The run and its charges, with the number and name of each asset.
        var run = await runs.GetAsync(q.Id, ct);
        var rows = await entryRows.ListForRunAsync(q.Id, ct);
        var ids = rows.Select(r => r.AssetId).ToList();
        var names = await assets.MapNumbersAndNamesAsync(ids, ct);

        // 2. The charges by asset number.
        var entries = rows.Select(r => new DepreciationEntryDto(r.AssetId, names.GetValueOrDefault(r.AssetId)?.Number ?? "?", names.GetValueOrDefault(r.AssetId)?.Name ?? "?", r.Amount, r.AccumulatedAfter)).OrderBy(e => e.Number).ToList();
        return DepreciationRunDto.From(run, entries, alreadyRun: true);
    }
}
