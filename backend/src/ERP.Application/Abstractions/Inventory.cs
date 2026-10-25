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
    Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default);
    Task<Guid?> IssueStockAsync(StockIssueRequest request, CancellationToken ct = default);
    Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default);
}
