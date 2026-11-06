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
    bool SkipJournal = false);

public sealed record StockIssueRequest(
    Guid LegalEntityId,
    Guid ItemId,
    decimal Quantity,
    DateOnly Date,
    string? Reference = null,
    string? Memo = null,
    Guid? WarehouseId = null,
    Guid? ExpenseAccountId = null,
    bool SkipJournal = false);

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
}
