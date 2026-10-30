using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Features;

/// <summary>Quantity, value and moving-average cost of one item in one warehouse.</summary>
public sealed record StockPosition(decimal Quantity, decimal Value)
{
    public decimal AverageCost => Quantity > 0 ? Math.Round(Value / Quantity, 4) : 0m;
}

/// <summary>Replays the movements of one item/warehouse in order to get the moving weighted-average cost (done in memory: SQL cannot add decimals portably).</summary>
public static class StockCosting
{
    public static StockPosition Replay(IEnumerable<StockMovement> movements)
    {
        decimal qty = 0, value = 0;
        foreach (var m in movements.OrderBy(m => m.Date).ThenBy(m => m.CreatedAt))
        {
            if (m.Quantity > 0) { qty += m.Quantity; value += m.Quantity * m.UnitCost; }
            else
            {
                var cost = qty > 0 ? value / qty : m.UnitCost;
                qty += m.Quantity; value += m.Quantity * cost; // Quantity is negative
                if (qty <= 0) { qty = Math.Max(qty, 0); value = qty == 0 ? 0 : value; }
            }
        }
        return new(qty, Math.Round(value, 4));
    }

    /// <summary>First date on which the running quantity would be negative once <paramref name="candidate"/> is added (candidate sorts last on its date), or null.</summary>
    public static DateOnly? FirstShortfall(IEnumerable<StockMovement> existing, StockMovement candidate)
    {
        var rows = existing.Select(m => (Date: m.Date, Created: m.CreatedAt, Quantity: m.Quantity)).Append((Date: candidate.Date, Created: DateTime.MaxValue, Quantity: candidate.Quantity)).OrderBy(r => r.Date).ThenBy(r => r.Created);
        decimal qty = 0;
        foreach (var r in rows)
        {
            qty += r.Quantity;
            if (qty < 0) return r.Date;
        }
        return null;
    }
}
