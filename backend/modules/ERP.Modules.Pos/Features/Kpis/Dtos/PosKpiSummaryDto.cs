namespace ERP.Modules.Pos.Features;

public record PosKpiSummaryDto(
    decimal TodaySales,
    int ActiveRegistersCount,
    int OpenSessionsCount,
    int TotalOrdersToday,
    decimal AverageTicket,
    decimal TodayReturns = 0);
