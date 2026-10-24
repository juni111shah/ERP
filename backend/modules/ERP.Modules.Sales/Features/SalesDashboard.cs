using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Sales.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

public sealed class SalesDashboard(
    IReadRepository<SalesOrder> orders,
    IReadRepository<SalesQuote> quotes,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => SalesModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();
        var charts = new List<DashboardChart>();

        if (user.HasPermission(P.OrdersRead))
        {
            var openOrdersQuery = orders.Query().Where(o => (o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing)
                && (entity == null || o.LegalEntityId == entity));
            var openOrdersCount = await openOrdersQuery.CountAsync(ct);
            var openOrdersAmount = await openOrdersQuery.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

            kpis.Add(new("open-sales-orders", "Open Orders", openOrdersCount, "number", $"backlog {openOrdersAmount:N0}", 110, "/sales/orders"));

            var totalSales = await orders.Query().Where(o => o.Status != SalesOrderStatus.Cancelled && (entity == null || o.LegalEntityId == entity))
                .SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            kpis.Add(new("total-sales-volume", "Total Sales", totalSales, "currency", "lifetime order book", 115, "/sales/analytics"));

            var confirmed = await orders.CountAsync(o => o.Status == SalesOrderStatus.Confirmed && (entity == null || o.LegalEntityId == entity), ct);
            var processing = await orders.CountAsync(o => o.Status == SalesOrderStatus.Processing && (entity == null || o.LegalEntityId == entity), ct);
            var delivered = await orders.CountAsync(o => o.Status == SalesOrderStatus.Delivered && (entity == null || o.LegalEntityId == entity), ct);
            var invoiced = await orders.CountAsync(o => o.Status == SalesOrderStatus.Invoiced && (entity == null || o.LegalEntityId == entity), ct);
            var draft = await orders.CountAsync(o => o.Status == SalesOrderStatus.Draft && (entity == null || o.LegalEntityId == entity), ct);

            charts.Add(new("sales-pipeline", "Sales orders by status", "hbar",
                ["Draft", "Confirmed", "Processing", "Delivered", "Invoiced"],
                [new("Orders count", [(decimal)draft, (decimal)confirmed, (decimal)processing, (decimal)delivered, (decimal)invoiced])],
                "Fulfillment status distribution", 25));
        }

        if (user.HasPermission(P.QuotesRead))
        {
            var openQuotes = await quotes.CountAsync(q => (q.Status == SalesQuoteStatus.Draft || q.Status == SalesQuoteStatus.Sent)
                && (entity == null || q.LegalEntityId == entity), ct);
            kpis.Add(new("open-sales-quotes", "Active Quotes", openQuotes, "number", "pipeline proposals", 120, "/sales/quotes"));
        }

        return new DashboardContribution(kpis, charts, []);
    }
}
