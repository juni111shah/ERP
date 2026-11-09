using ERP.Modules.Pos.Domain;

namespace ERP.Modules.Pos.Features;

/// <summary>The "today" filter shared by the KPI endpoint and the dashboard card: completed (never voided) orders since the shop's midnight.</summary>
public static class PosToday
{
    public static IQueryable<PosOrder> CompletedOrders(IQueryable<PosOrder> orders, Guid? legalEntityId, DateTime startOfDayUtc)
    {
        // CreatedAt (stamped with the order date, UTC) compares in SQL on every provider; DateTimeOffset comparisons do not translate on SQLite.
        return orders.Where(o => o.CreatedAt >= startOfDayUtc && o.Status == PosOrderStatus.Completed && (legalEntityId == null || o.LegalEntityId == legalEntityId));
    }
}
