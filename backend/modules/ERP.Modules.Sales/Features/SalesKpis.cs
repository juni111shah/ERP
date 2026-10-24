using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modules.Sales.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Sales.Features;

public sealed record SalesKpiDto(
    decimal TotalSales,
    decimal OpenOrdersAmount,
    int OpenOrdersCount,
    decimal OpenQuotesAmount,
    int OpenQuotesCount,
    decimal ConversionRate,
    int TotalQuotesCount,
    int TotalOrdersCount);

public sealed record GetSalesKpiSummary() : IQuery<SalesKpiDto>, IRequirePermission
{
    public string RequiredPermission => P.OrdersRead;
}

internal sealed class SalesKpiHandlers(
    IReadRepository<SalesOrder> orders,
    IReadRepository<SalesQuote> quotes) :
    IRequestHandler<GetSalesKpiSummary, SalesKpiDto>
{
    public async Task<SalesKpiDto> Handle(GetSalesKpiSummary q, CancellationToken ct)
    {
        var totalOrders = await orders.Query().CountAsync(ct);
        var openOrdersQuery = orders.Query().Where(o => o.Status == SalesOrderStatus.Confirmed || o.Status == SalesOrderStatus.Processing);
        var openOrdersCount = await openOrdersQuery.CountAsync(ct);
        var openOrdersAmount = await openOrdersQuery.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
        var totalSales = await orders.Query().Where(o => o.Status != SalesOrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;

        var totalQuotes = await quotes.Query().CountAsync(ct);
        var openQuotesQuery = quotes.Query().Where(q => q.Status == SalesQuoteStatus.Draft || q.Status == SalesQuoteStatus.Sent);
        var openQuotesCount = await openQuotesQuery.CountAsync(ct);
        var openQuotesAmount = await openQuotesQuery.SumAsync(q => (decimal?)q.TotalAmount, ct) ?? 0m;

        var convertedQuotes = await quotes.Query().CountAsync(q => q.Status == SalesQuoteStatus.Converted, ct);
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
