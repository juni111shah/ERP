namespace ERP.Application.Finance;

/// <summary>An expected cash movement on a date: positive = money in, negative = money out.</summary>
public sealed record ForecastItem(DateOnly Date, decimal Amount);

/// <summary>Where expected cash comes from. Core supplies cheques, receivables and bills; modules add their own (payroll, loan instalments).</summary>
public interface ICashForecastSource
{
    string Key { get; }
    string Label { get; }
    /// <summary>Null for core sources; a module key for sources that only apply while that module is enabled.</summary>
    string? ModuleKey { get; }
    Task<IReadOnlyList<ForecastItem>> ItemsAsync(Guid? legalEntityId, DateOnly asOf, DateOnly horizon, CancellationToken ct);
}
