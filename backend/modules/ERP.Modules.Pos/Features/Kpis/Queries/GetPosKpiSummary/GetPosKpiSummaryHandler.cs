using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using ERP.Modules.Pos.Repositories;

namespace ERP.Modules.Pos.Features;

internal sealed class GetPosKpiSummaryHandler(
    IPosOrderRepository orders,
    IPosRegisterRepository registers,
    IPosSessionRepository sessions,
    IPosReturnRepository returns,
    PosCalendar calendar) : IQueryHandler<GetPosKpiSummaryQuery, PosKpiSummaryDto>
{
    public async Task<PosKpiSummaryDto> Handle(GetPosKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        // 1. Since the shop's midnight.
        var entity = request.LegalEntityId;
        var day = await calendar.TodayAsync(cancellationToken);
        var (start, _) = await calendar.RangeAsync(day, day, cancellationToken);

        // 2. Today's sales and returns.
        var totalOrders = await orders.CountCompletedSinceAsync(entity, start, cancellationToken);
        var todaySales = await orders.TotalCompletedSinceAsync(entity, start, cancellationToken);
        var todayReturns = await returns.TotalSinceAsync(entity, start, cancellationToken);

        // 3. The registers and shifts.
        var activeRegistersCount = await registers.CountAsync(r => r.IsActive && (entity == null || r.LegalEntityId == entity), cancellationToken);
        var openSessionsCount = await sessions.CountAsync(s => s.Status == PosSessionStatus.Open && (entity == null || s.LegalEntityId == entity), cancellationToken);
        var avgTicket = totalOrders > 0 ? todaySales / totalOrders : 0m;

        return new PosKpiSummaryDto(todaySales, activeRegistersCount, openSessionsCount, totalOrders, Math.Round(avgTicket, 2), todayReturns);
    }
}
