using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Modules.Pos.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public record PosKpiSummaryDto(
    decimal TodaySales,
    int ActiveRegistersCount,
    int OpenSessionsCount,
    int TotalOrdersToday,
    decimal AverageTicket,
    decimal TodayReturns = 0);

public record GetPosKpiSummaryQuery(Guid? LegalEntityId = null) : IQuery<PosKpiSummaryDto>;

/// <summary>The "today" filter shared by the KPI endpoint and the dashboard card: completed (never voided) orders since the shop's midnight.</summary>
public static class PosToday
{
    public static IQueryable<PosOrder> CompletedOrders(IQueryable<PosOrder> orders, Guid? legalEntityId, DateTime startOfDayUtc)
    {
        // CreatedAt (stamped with the order date, UTC) compares in SQL on every provider; DateTimeOffset comparisons do not translate on SQLite.
        return orders.Where(o => o.CreatedAt >= startOfDayUtc && o.Status == PosOrderStatus.Completed && (legalEntityId == null || o.LegalEntityId == legalEntityId));
    }
}

public class PosKpiHandlers : IRequestHandler<GetPosKpiSummaryQuery, PosKpiSummaryDto>
{
    private readonly IRepository<PosOrder> _orders;
    private readonly IRepository<PosRegister> _registers;
    private readonly IRepository<PosSession> _sessions;
    private readonly IRepository<PosReturn> _returns;
    private readonly PosCalendar _calendar;

    public PosKpiHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosRegister> registers,
        IRepository<PosSession> sessions,
        IRepository<PosReturn> returns,
        PosCalendar calendar)
    {
        _orders = orders;
        _registers = registers;
        _sessions = sessions;
        _returns = returns;
        _calendar = calendar;
    }

    public async Task<PosKpiSummaryDto> Handle(GetPosKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        var entity = request.LegalEntityId;
        var day = await _calendar.TodayAsync(cancellationToken);
        var (start, _) = await _calendar.RangeAsync(day, day, cancellationToken);
        var today = PosToday.CompletedOrders(_orders.Query(), entity, start);
        var totalOrders = await today.CountAsync(cancellationToken);
        // summed as double so SQLite (tests) can aggregate in SQL too; rounded back to money
        var todaySales = Math.Round((decimal)(await today.SumAsync(o => (double?)o.TotalAmount, cancellationToken) ?? 0d), 2);
        var todayReturns = Math.Round((decimal)(await _returns.Query().Where(r => r.CreatedAt >= start && (entity == null || r.LegalEntityId == entity)).SumAsync(r => (double?)r.TotalAmount, cancellationToken) ?? 0d), 2);

        var activeRegistersCount = await _registers.CountAsync(r => r.IsActive && (entity == null || r.LegalEntityId == entity), cancellationToken);
        var openSessionsCount = await _sessions.CountAsync(s => s.Status == PosSessionStatus.Open && (entity == null || s.LegalEntityId == entity), cancellationToken);
        var avgTicket = totalOrders > 0 ? todaySales / totalOrders : 0m;

        return new PosKpiSummaryDto(todaySales, activeRegistersCount, openSessionsCount, totalOrders, Math.Round(avgTicket, 2), todayReturns);
    }
}
