using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.IndustryPacks.Repositories;

namespace ERP.Modules.IndustryPacks.Features;

internal sealed class GetRecordSummaryHandler(IIndustryRecordRepository records, ICurrentUser user) : IQueryHandler<GetRecordSummaryQuery, IReadOnlyList<RecordTypeSummary>>
{
    public async Task<IReadOnlyList<RecordTypeSummary>> Handle(GetRecordSummaryQuery q, CancellationToken ct)
    {
        // 1. The readable types and the status and amount of their records.
        var pack = PackCatalog.Require(q.Module);
        var readable = pack.Types.Where(t => user.HasPermission(PackModule.ReadPermission(pack.Key, t.Key))).ToList();
        var keys = readable.Select(t => t.Key).ToArray();
        var all = await records.ListStatusAmountsAsync(pack.Key, keys, q.LegalEntityId, ct);

        // 2. Count and amount per type and status (added up here: decimal sums are not portable).
        var rows = all.GroupBy(r => new { r.RecordType, r.Status }).Select(g => new { g.Key.RecordType, g.Key.Status, Count = g.Count(), Amount = g.Sum(x => x.Amount) }).ToList();
        return readable.Select(t =>
        {
            var mine = rows.Where(r => r.RecordType == t.Key).ToList();
            return new RecordTypeSummary(t.Key, t.Label, mine.Sum(r => r.Count), mine.Sum(r => r.Amount), mine.Select(r => new RecordStatusCount(r.Status, r.Count, r.Amount)).ToList());
        }).ToList();
    }
}
