using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Pos.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Pos.Features;

public sealed class PosDashboard(
    IReadRepository<PosOrder> orders,
    IReadRepository<PosSession> sessions,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => PosModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();

        if (user.HasPermission(Permissions.OrdersRead))
        {
            var startOfDay = DateTimeOffset.UtcNow.Date;
            var todayOrdersQuery = orders.Query().Where(o => o.OrderDate >= startOfDay && o.Status == PosOrderStatus.Completed
                && (entity == null || o.LegalEntityId == entity));

            var todaySales = await todayOrdersQuery.SumAsync(o => (decimal?)o.TotalAmount, ct) ?? 0m;
            var todayCount = await todayOrdersQuery.CountAsync(ct);

            kpis.Add(new("pos-today-sales", "POS Today Sales", todaySales, "currency", $"{todayCount} counter sales", 140, "/pos/terminal"));
        }

        if (user.HasPermission(Permissions.SessionsRead))
        {
            var openSessions = await sessions.CountAsync(s => s.Status == PosSessionStatus.Open
                && (entity == null || s.LegalEntityId == entity), ct);

            kpis.Add(new("pos-open-sessions", "Active POS Shifts", openSessions, "number", "registers in operation", 145, "/pos/sessions"));
        }

        return new DashboardContribution(kpis, [], []);
    }
}
