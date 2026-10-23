using ERP.Domain.Common;

namespace ERP.Modules.Inventory.Domain;

public enum MovementKind { Receipt = 1, Issue = 2, Adjustment = 3, TransferIn = 4, TransferOut = 5 }

/// <summary>
/// One line of the stock ledger. Quantity is signed (positive in, negative out); stock on hand is the sum of movements, never a stored balance.
/// UnitCost is the cost per unit at that moment (moving weighted average for issues and transfers).
/// </summary>
[DefaultSort("-date")]
public class StockMovement : LegalEntityScopedEntity
{
    [Filterable] public Guid WarehouseId { get; set; }
    [Filterable] public Guid ItemId { get; set; }
    [Filterable, Sortable] public DateOnly Date { get; set; }
    [Filterable] public MovementKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    [Searchable] public string? Reference { get; set; }
    [Searchable] public string? Memo { get; set; }
    public Guid? JournalEntryId { get; set; }
    /// <summary>Ties the two sides of a transfer together.</summary>
    public Guid? TransferId { get; set; }
}

/// <summary>Reorder rule of an item: when stock (all warehouses) falls to the level, it shows up as low stock.</summary>
public class StockPolicy : OrgEntity
{
    public Guid ItemId { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
}
