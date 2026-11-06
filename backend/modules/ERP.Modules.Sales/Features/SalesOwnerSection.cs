using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

/// <summary>Sales performance for the owner: orders taken in the period and the customers behind them.</summary>
public sealed class SalesOwnerSection(IReadRepository<SalesOrder> orders, IReadRepository<SalesQuote> quotes, ICurrentUser user) : IOwnerDashboardSection
{
    public string ModuleKey => SalesModule.Key;

    public async Task<IReadOnlyList<OwnerSection>> BuildAsync(OwnerScope scope, CancellationToken ct)
    {
        if (!user.HasPermission(P.OrdersRead)) return [];
        var entity = scope.LegalEntityId;
        var rows = await orders.Query().Where(o => o.Status != SalesOrderStatus.Cancelled && o.OrderDate >= scope.From && o.OrderDate <= scope.To && (entity == null || o.LegalEntityId == entity))
            .Select(o => new { o.CustomerName, o.TotalAmount }).ToListAsync(ct);
        var allQuotes = await quotes.Query().Where(q => q.QuoteDate >= scope.From && q.QuoteDate <= scope.To && (entity == null || q.LegalEntityId == entity)).Select(q => q.Status).ToListAsync(ct);
        var won = allQuotes.Count(s => s is SalesQuoteStatus.Accepted or SalesQuoteStatus.Converted);
        var top = rows.GroupBy(r => r.CustomerName).Select(g => new { Name = g.Key, Value = g.Sum(r => r.TotalAmount), Count = g.Count() }).OrderByDescending(g => g.Value).Take(6).ToList();
        return
        [
            new("sales", "Sales performance",
                [new("sales-orders", "Orders in period", rows.Count, "number", Link: "/sales/orders"), new("sales-value", "Order value", rows.Sum(r => r.TotalAmount)),
                 new("quote-win", "Quotes won", allQuotes.Count == 0 ? 0 : Math.Round(won * 100m / allQuotes.Count, 1), "percent", $"{won} of {allQuotes.Count}", "/sales/quotes")],
                new(["Customer", "Orders", "Value"], top.Select(t => (IReadOnlyList<string>)[t.Name, t.Count.ToString(), t.Value.ToString("0.00")]).ToList(), ["text", "number", "money"])),
        ];
    }
}
