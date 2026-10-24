using ERP.Application.Abstractions;
using ERP.Application.Search;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

public sealed class SalesSearch(
    IReadRepository<SalesOrder> orders,
    IReadRepository<SalesQuote> quotes,
    ICurrentUser user) : ISearchProvider
{
    public string? ModuleKey => SalesModule.Key;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string term, int limit, CancellationToken ct)
    {
        var hits = new List<SearchHit>();

        if (user.HasPermission(P.OrdersRead))
        {
            var oList = await orders.Query(new Specification<SalesOrder>(o =>
                o.Number.Contains(term) || o.CustomerName.Contains(term) || (o.CustomerReference != null && o.CustomerReference.Contains(term))))
                .Take(limit)
                .Select(o => new { o.Id, o.Number, o.CustomerName, o.Status, o.TotalAmount, o.Currency })
                .ToListAsync(ct);

            hits.AddRange(oList.Select(o => new SearchHit("Sales Orders", $"{o.Number} · {o.CustomerName}", $"{o.Currency} {o.TotalAmount:N2} · {o.Status}", $"/sales/orders/{o.Id}")));
        }

        if (user.HasPermission(P.QuotesRead) && hits.Count < limit)
        {
            var remaining = limit - hits.Count;
            var qList = await quotes.Query(new Specification<SalesQuote>(q =>
                q.Number.Contains(term) || q.CustomerName.Contains(term)))
                .Take(remaining)
                .Select(q => new { q.Id, q.Number, q.CustomerName, q.Status, q.TotalAmount, q.Currency })
                .ToListAsync(ct);

            hits.AddRange(qList.Select(q => new SearchHit("Sales Quotes", $"{q.Number} · {q.CustomerName}", $"{q.Currency} {q.TotalAmount:N2} · {q.Status}", $"/sales/quotes/{q.Id}")));
        }

        return hits;
    }
}
