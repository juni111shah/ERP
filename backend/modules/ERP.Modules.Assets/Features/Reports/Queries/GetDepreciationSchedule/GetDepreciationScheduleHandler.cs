using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Modules.Assets.Repositories;

namespace ERP.Modules.Assets.Features;

internal sealed class GetDepreciationScheduleHandler(IFixedAssetRepository assets, IDepreciationEntryRepository entries, IReportCurrencyGuard currencies)
    : IQueryHandler<GetDepreciationScheduleQuery, DepreciationForecastDto>
{
    public async Task<DepreciationForecastDto> Handle(GetDepreciationScheduleQuery q, CancellationToken ct)
    {
        // 1. One currency only; whole months; what was posted in them and the assets still depreciating.
        await currencies.EnsureSingleCurrencyAsync(q.LegalEntityId, ct);
        var from = new DateOnly(q.From.Year, q.From.Month, 1);
        var to = DepreciationCalculator.EndOfMonth(q.To.Year, q.To.Month);
        var posted = await entries.ListAmountsAsync(from, to, q.LegalEntityId, ct);
        var open = await assets.ListCapitalisedAsync(q.LegalEntityId, ct);

        // 2. What the schedules still expect per month.
        var planned = new Dictionary<DateOnly, decimal>();
        foreach (var a in open)
            foreach (var p in DepreciationCalculator.Schedule(DepreciationCalculator.PlanOf(a)).Where(p => p.Accumulated > a.AccumulatedDepreciation && p.PeriodEnd >= from && p.PeriodEnd <= to))
                planned[p.PeriodEnd] = planned.GetValueOrDefault(p.PeriodEnd) + p.Amount;

        // 3. One row per month with anything posted or planned.
        var rows = new List<DepreciationPeriodRow>();
        for (var month = from; month <= to; month = month.AddMonths(1))
        {
            var end = DepreciationCalculator.EndOfMonth(month.Year, month.Month);
            var (done, ahead) = (posted.Where(p => p.PeriodEnd == end).Sum(p => p.Amount), planned.GetValueOrDefault(end));
            if (done != 0 || ahead != 0) rows.Add(new DepreciationPeriodRow(month.Year, month.Month, done, ahead));
        }
        return new DepreciationForecastDto(from, to, rows, rows.Sum(r => r.Posted), rows.Sum(r => r.Planned));
    }
}
