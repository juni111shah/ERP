using System.Linq.Expressions;
using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Features;

public sealed record MovementDto(Guid Id, Guid WarehouseId, Guid ItemId, DateOnly Date, MovementKind Kind, decimal Quantity, decimal UnitCost, string? Reference, string? Memo, Guid? JournalEntryId)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<StockMovement, MovementDto>> Projection = m => new MovementDto(m.Id, m.WarehouseId, m.ItemId, m.Date, m.Kind, m.Quantity, m.UnitCost, m.Reference, m.Memo, m.JournalEntryId);

    public static MovementDto From(StockMovement m) => new(m.Id, m.WarehouseId, m.ItemId, m.Date, m.Kind, m.Quantity, m.UnitCost, m.Reference, m.Memo, m.JournalEntryId);
}
