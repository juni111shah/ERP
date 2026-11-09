using ERP.Application.Abstractions;
using ERP.Modules.Sales.Domain;

namespace ERP.Modules.Sales.Repositories;

/// <summary>Data access for sales quotes. Implemented in SalesQuoteRepository.cs.</summary>
public interface ISalesQuoteRepository : IRepository<SalesQuote>
{
    /// <summary>The quote with its lines, tracked for changes; null when not found.</summary>
    Task<SalesQuote?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>How many quotes are open (draft or sent).</summary>
    Task<int> CountOpenAsync(CancellationToken ct);

    /// <summary>The total of the open quotes (draft or sent).</summary>
    Task<decimal> TotalOpenAsync(CancellationToken ct);
}
