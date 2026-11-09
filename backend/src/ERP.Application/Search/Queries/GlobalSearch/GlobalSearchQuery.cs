using ERP.Application.Common;

namespace ERP.Application.Search;

/// <summary>Global search across every enabled provider (at least two characters).</summary>
public sealed record GlobalSearchQuery(string Query) : IQuery<SearchResult>;
