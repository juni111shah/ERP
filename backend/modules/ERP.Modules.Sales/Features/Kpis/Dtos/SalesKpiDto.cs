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
