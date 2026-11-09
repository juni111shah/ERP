
namespace ERP.Application.Search;

/// <summary>One match for the global search box: a labelled link to a record.</summary>
public sealed record SearchHit(string Category, string Title, string? Subtitle, string Link);

/// <summary>Searches one kind of record. Core and each module register providers; a provider returns nothing for callers who may not read its records.</summary>
public interface ISearchProvider
{
    /// <summary>Module that must be enabled; null = core.</summary>
    string? ModuleKey { get; }
    Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct);
}

public sealed record SearchResult(string Query, IReadOnlyList<SearchHit> Hits);
