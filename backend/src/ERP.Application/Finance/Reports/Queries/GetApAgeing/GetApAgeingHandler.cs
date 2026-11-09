using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetApAgeingHandler(PayablesAsOf payables, IReportCurrencyGuard currencies) : IQueryHandler<GetApAgeingQuery, ArAgeingDto>
{
    public async Task<ArAgeingDto> Handle(GetApAgeingQuery query, CancellationToken ct)
    {
        // 1. One currency only; open bills per vendor, less the vendor advances.
        await currencies.EnsureSingleCurrencyAsync(query.LegalEntityId, ct);
        var open = await payables.OpenAsync(query.AsOf, query.LegalEntityId, null, ct);
        var rows = new Dictionary<Guid, AgeingRow>();
        foreach (var o in open) rows[o.PartyId] = Ageing.Add(rows.GetValueOrDefault(o.PartyId) ?? new AgeingRow(o.PartyId, o.PartyName, 0, 0, 0, 0, 0, 0), query.AsOf, o.DueDate, o.Total - o.Paid);
        foreach (var (party, name, amount) in await payables.AdvancesAsync(query.AsOf, query.LegalEntityId, null, ct)) rows[party] = Ageing.Add(rows.GetValueOrDefault(party) ?? new AgeingRow(party, name, 0, 0, 0, 0, 0, 0), query.AsOf, query.AsOf, -amount);

        // 2. Non-zero rows by name, and the totals.
        var list = rows.Values.Where(r => r.Total != 0).OrderBy(r => r.PartyName).ToList();
        var totals = new AgeingRow(Guid.Empty, "Total", list.Sum(r => r.Current), list.Sum(r => r.Days1To30), list.Sum(r => r.Days31To60), list.Sum(r => r.Days61To90), list.Sum(r => r.Over90), list.Sum(r => r.Total));
        return new ArAgeingDto(query.AsOf, list, totals);
    }
}
