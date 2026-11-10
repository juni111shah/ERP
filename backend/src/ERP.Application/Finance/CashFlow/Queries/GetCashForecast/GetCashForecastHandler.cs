using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Finance;

internal sealed class GetCashForecastHandler(CashMovementsCalculator movements, IEnumerable<ICashForecastSource> sources, IModuleAccess modules, IClock clock)
    : IQueryHandler<GetCashForecastQuery, CashForecastDto>
{
    public async Task<CashForecastDto> Handle(GetCashForecastQuery q, CancellationToken ct)
    {
        // 1. The weeks, from Monday of this week, and the cash in the bank today.
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var start = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday of this week
        var weeks = Math.Clamp(q.Weeks, 1, 52);
        var horizon = start.AddDays(weeks * 7 - 1);
        var cash = (await movements.ComputeAsync(DateOnly.FromDateTime(clock.UtcNow), today, q.LegalEntityId, ct)).Closing;

        // 2. The expected items of every source that applies (module sources only while their module is enabled).
        var active = new List<(ICashForecastSource S, IReadOnlyList<ForecastItem> Items)>();
        foreach (var s in sources)
        {
            if (s.ModuleKey is { } key && !await modules.IsEnabledAsync(key, ct)) continue;
            active.Add((s, await s.ItemsAsync(q.LegalEntityId, today, horizon, ct)));
        }

        // 3. Week by week, with the projected balance.
        var result = new List<ForecastWeek>(); var balance = cash;
        for (var w = 0; w < weeks; w++)
        {
            var from = start.AddDays(w * 7); var to = from.AddDays(6);
            var amounts = active.ToDictionary(a => a.S.Key, a => a.Items.Where(i => (w == 0 ? i.Date <= to : i.Date >= from && i.Date <= to)).Sum(i => i.Amount));
            var net = amounts.Values.Sum(); balance += net;
            result.Add(new ForecastWeek(from, amounts, net, balance));
        }
        return new CashForecastDto(today, cash, active.Select(a => new ForecastSourceInfo(a.S.Key, a.S.Label)).ToList(), result, balance, Math.Min(cash, result.Min(w => w.Projected)));
    }
}
