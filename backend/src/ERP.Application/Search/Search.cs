using ERP.Application.Abstractions;
using ERP.Application.Common;
using MediatR;

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

/// <summary>Global search across every enabled provider (at least two characters).</summary>
public sealed record GlobalSearch(string Query) : IQuery<SearchResult>;

internal sealed class SearchHandler(IEnumerable<ISearchProvider> providers, IModuleAccess modules) : IRequestHandler<GlobalSearch, SearchResult>
{
    private const int PerProvider = 5;

    public async Task<SearchResult> Handle(GlobalSearch q, CancellationToken ct)
    {
        var term = (q.Query ?? "").Trim();
        if (term.Length < 2) return new SearchResult(term, []);
        var hits = new List<SearchHit>();
        foreach (var p in providers)
        {
            if (p.ModuleKey is { } key && !await modules.IsEnabledAsync(key, ct)) continue;
            hits.AddRange(await p.SearchAsync(term, PerProvider, ct));
        }
        return new SearchResult(term, hits);
    }
}
