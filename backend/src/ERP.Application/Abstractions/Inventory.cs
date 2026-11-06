namespace ERP.Application.Abstractions;

public sealed record StockReceiveRequest(
    Guid LegalEntityId,
    Guid ItemId,
    decimal Quantity,
    decimal UnitCost,
    DateOnly Date,
    string? Reference = null,
    string? Memo = null,
    Guid? WarehouseId = null,
    Guid? OffsetAccountId = null,
    bool SkipJournal = false,
    /// <summary>Refuse (409) when the legal entity has no active warehouse, instead of returning null with nothing moved.</summary>
    bool RequireWarehouse = false,
    /// <summary>Set by <see cref="IStockPostingService.ReturnIssuedStockAsync"/>: the issue reference this receipt gives back.</summary>
    string? ReturnOfReference = null);

public sealed record StockIssueRequest(
    Guid LegalEntityId,
    Guid ItemId,
    decimal Quantity,
    DateOnly Date,
    string? Reference = null,
    string? Memo = null,
    Guid? WarehouseId = null,
    Guid? ExpenseAccountId = null,
    bool SkipJournal = false,
    /// <summary>
    /// Document-driven issues set this: the issue is refused (409) when it would take the item below zero in that warehouse, on its date or later,
    /// or when there is no warehouse to issue from. The workspace setting inventory.allowNegativeStock (default off) lifts the quantity check. Off by default so POS keeps its own setting.
    /// </summary>
    bool EnforceAvailable = false);

/// <summary>
/// Service allowing other modules (Purchasing, Sales, POS, Manufacturing, Distribution)
/// to receive or issue inventory stock when the Inventory module is enabled.
/// </summary>
public interface IStockPostingService
{
    Task<bool> IsInventoryEnabledAsync(CancellationToken ct = default);
    /// <summary>True when the warehouse exists, is active and belongs to the legal entity. Always true when the Inventory module is off (nothing is stocked then).</summary>
    Task<bool> IsWarehouseUsableAsync(Guid warehouseId, Guid legalEntityId, CancellationToken ct = default);
    Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default);
    Task<Guid?> IssueStockAsync(StockIssueRequest request, CancellationToken ct = default);
    Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default);
    Task ReverseReceiptAsync(string reference, CancellationToken ct = default);
    Task ReverseIssueAsync(string reference, CancellationToken ct = default);
    /// <summary>Quantity on hand per stock-tracked item in the warehouse (or the legal entity's first warehouse). Items not tracked, and everything when the Inventory module is off, are left out.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Guid> itemIds, Guid? warehouseId, Guid legalEntityId, CancellationToken ct = default);
    /// <summary>Puts part of an earlier issue back on the shelf at the cost it left at, so cost of sales reverses exactly. Returns null when that issue moved no stock.</summary>
    Task<Guid?> ReturnIssuedStockAsync(string issueReference, Guid itemId, decimal quantity, string returnReference, DateOnly date, CancellationToken ct = default);
    /// <summary>Value (quantity times cost, 2 decimals, always positive) of a movement this service wrote; 0 when there is none. For callers that post their own journal with SkipJournal.</summary>
    Task<decimal> GetMovementValueAsync(Guid movementId, CancellationToken ct = default);
}
