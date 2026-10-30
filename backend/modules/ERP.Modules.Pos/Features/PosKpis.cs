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
    decimal AverageTicket);

public record GetPosKpiSummaryQuery(Guid? LegalEntityId = null) : IQuery<PosKpiSummaryDto>;

/// <summary>The "today" filter shared by the KPI endpoint and the dashboard card: completed (never voided) orders since 00:00 UTC.</summary>
public static class PosToday
{
    public static IQueryable<PosOrder> CompletedOrders(IQueryable<PosOrder> orders, Guid? legalEntityId)
    {
        // CreatedAt (stamped with the order date, UTC) compares in SQL on every provider; DateTimeOffset comparisons do not translate on SQLite.
        var startOfDay = DateTime.UtcNow.Date;
        return orders.Where(o => o.CreatedAt >= startOfDay && o.Status == PosOrderStatus.Completed && (legalEntityId == null || o.LegalEntityId == legalEntityId));
    }
}

public class PosKpiHandlers : IRequestHandler<GetPosKpiSummaryQuery, PosKpiSummaryDto>
{
    private readonly IRepository<PosOrder> _orders;
    private readonly IRepository<PosRegister> _registers;
    private readonly IRepository<PosSession> _sessions;

    public PosKpiHandlers(
        IRepository<PosOrder> orders,
        IRepository<PosRegister> registers,
        IRepository<PosSession> sessions)
    {
        _orders = orders;
        _registers = registers;
        _sessions = sessions;
    }

    public async Task<PosKpiSummaryDto> Handle(GetPosKpiSummaryQuery request, CancellationToken cancellationToken)
    {
        var entity = request.LegalEntityId;
        var today = PosToday.CompletedOrders(_orders.Query(), entity);
        var totalOrders = await today.CountAsync(cancellationToken);
        // summed as double so SQLite (tests) can aggregate in SQL too; rounded back to money
        var todaySales = Math.Round((decimal)(await today.SumAsync(o => (double?)o.TotalAmount, cancellationToken) ?? 0d), 2);

        var activeRegistersCount = await _registers.CountAsync(r => r.IsActive && (entity == null || r.LegalEntityId == entity), cancellationToken);
        var openSessionsCount = await _sessions.CountAsync(s => s.Status == PosSessionStatus.Open && (entity == null || s.LegalEntityId == entity), cancellationToken);
        var avgTicket = totalOrders > 0 ? todaySales / totalOrders : 0m;

        return new PosKpiSummaryDto(todaySales, activeRegistersCount, openSessionsCount, totalOrders, Math.Round(avgTicket, 2));
    }
}
