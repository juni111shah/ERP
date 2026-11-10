using ERP.Application.Common;
using ERP.Modules.Sales.Domain;
using ERP.Modules.Sales.Repositories;

namespace ERP.Modules.Sales.Features;

internal sealed class GetSalesKpiSummaryHandler(ISalesOrderRepository orders, ISalesQuoteRepository quotes) : IQueryHandler<GetSalesKpiSummaryQuery, SalesKpiDto>
{
    public async Task<SalesKpiDto> Handle(GetSalesKpiSummaryQuery q, CancellationToken ct)
    {
        // 1. Orders: all, open and sold.
        var totalOrders = await orders.CountAsync(ct: ct);
        var openOrdersCount = await orders.CountOpenAsync(ct);
        var openOrdersAmount = await orders.TotalOpenAsync(ct);
        var totalSales = await orders.TotalNotCancelledAsync(ct);

        // 2. Quotes: all, open and converted.
        var totalQuotes = await quotes.CountAsync(ct: ct);
        var openQuotesCount = await quotes.CountOpenAsync(ct);
        var openQuotesAmount = await quotes.TotalOpenAsync(ct);

        var convertedQuotes = await quotes.CountAsync(q => q.Status == SalesQuoteStatus.Converted, ct);
        var conversionRate = totalQuotes > 0 ? Math.Round((decimal)convertedQuotes / totalQuotes * 100m, 1) : 0m;

        return new SalesKpiDto(
            TotalSales: Math.Round(totalSales, 2),
            OpenOrdersAmount: Math.Round(openOrdersAmount, 2),
            OpenOrdersCount: openOrdersCount,
            OpenQuotesAmount: Math.Round(openQuotesAmount, 2),
            OpenQuotesCount: openQuotesCount,
            ConversionRate: conversionRate,
            TotalQuotesCount: totalQuotes,
            TotalOrdersCount: totalOrders);
    }
}
