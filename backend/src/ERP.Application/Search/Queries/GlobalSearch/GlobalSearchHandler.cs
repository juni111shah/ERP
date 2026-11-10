using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Search;

internal sealed class GlobalSearchHandler(IEnumerable<ISearchProvider> providers, IModuleAccess modules) : IQueryHandler<GlobalSearchQuery, SearchResult>
{
    private const int PerProvider = 5;

    public async Task<SearchResult> Handle(GlobalSearchQuery q, CancellationToken ct)
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
