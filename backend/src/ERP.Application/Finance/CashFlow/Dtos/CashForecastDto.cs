namespace ERP.Application.Finance;

public sealed record ForecastWeek(DateOnly WeekOf, IReadOnlyDictionary<string, decimal> Amounts, decimal Net, decimal Projected);
public sealed record CashForecastDto(DateOnly AsOf, decimal CashToday, IReadOnlyList<ForecastSourceInfo> Sources, IReadOnlyList<ForecastWeek> Weeks, decimal EndBalance, decimal LowestBalance);
public sealed record ForecastSourceInfo(string Key, string Label);
