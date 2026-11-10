using System.Linq.Expressions;
using ERP.Modules.Inventory.Domain;

namespace ERP.Modules.Inventory.Features;

public sealed record StockCountInput(Guid WarehouseId, DateOnly CountDate, string? Memo = null);
public sealed record CountedLineInput(Guid ItemId, decimal? CountedQuantity);
public sealed record StockCountLineDto(Guid ItemId, string Sku, string Item, string Uom, decimal SystemQuantity, decimal? CountedQuantity, decimal? Variance, Guid? MovementId);
public sealed record StockCountDto(Guid Id, string Number, Guid WarehouseId, string Warehouse, DateOnly CountDate, StockCountStatus Status, string? Memo, DateTime? PostedAt, IReadOnlyList<StockCountLineDto> Lines);

public sealed record StockCountRow(Guid Id, string Number, Guid WarehouseId, DateOnly CountDate, StockCountStatus Status, int Lines, string? Memo)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<StockCount, StockCountRow>> Projection = c => new StockCountRow(c.Id, c.Number, c.WarehouseId, c.CountDate, c.Status, c.Lines.Count, c.Memo);
}
