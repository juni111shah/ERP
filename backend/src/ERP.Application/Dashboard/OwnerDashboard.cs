using ERP.Application.Common;

namespace ERP.Application.Dashboard;

public sealed record OwnerCard(string Key, string Label, decimal Value, string Format = "money", string? Caption = null, string? Link = null);
public sealed record OwnerTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<string>? ColumnFormats = null);
public sealed record OwnerSeries(string Name, IReadOnlyList<decimal> Values);
public sealed record OwnerChart(IReadOnlyList<string> Categories, IReadOnlyList<OwnerSeries> Series);
/// <summary>One block of the owner's view. Every figure is computed on request from posted transactions and live documents.</summary>
public sealed record OwnerSection(string Key, string Title, IReadOnlyList<OwnerCard> Cards, OwnerTable? Table = null, OwnerChart? Chart = null, string? Note = null);
public sealed record OwnerDashboardDto(DateOnly From, DateOnly To, string? Currency, IReadOnlyList<OwnerSection> Sections);
public sealed record OwnerScope(Guid? LegalEntityId, DateOnly From, DateOnly To, DateOnly Today);

/// <summary>A module's part of the owner's view, such as stock, staff or project profit. Consulted only when the module is enabled.</summary>
public interface IOwnerDashboardSection
{
    string ModuleKey { get; }
    Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct);
}

