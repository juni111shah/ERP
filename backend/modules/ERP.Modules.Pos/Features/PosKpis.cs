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

public record GetPosKpiSummaryQuery : IQuery<PosKpiSummaryDto>;

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
        var startOfDay = DateTimeOffset.UtcNow.Date;
        var orders = await _orders.Query(new Specification<PosOrder>(
            o => o.OrderDate >= startOfDay && o.Status == PosOrderStatus.Completed))
            .ToListAsync(cancellationToken);

        var activeRegistersCount = await _registers.CountAsync(r => r.IsActive, cancellationToken);
        var openSessionsCount = await _sessions.CountAsync(s => s.Status == PosSessionStatus.Open, cancellationToken);

        var todaySales = orders.Sum(o => o.TotalAmount);
        var totalOrders = orders.Count;
        var avgTicket = totalOrders > 0 ? todaySales / totalOrders : 0m;

        return new PosKpiSummaryDto(
            todaySales,
            activeRegistersCount,
            openSessionsCount,
            totalOrders,
            Math.Round(avgTicket, 2));
    }
}
