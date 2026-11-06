using ERP.Application.Abstractions;

namespace ERP.Infrastructure.Services;

public sealed class NullStockPostingService : IStockPostingService
{
    public Task<bool> IsInventoryEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> IsWarehouseUsableAsync(Guid warehouseId, Guid legalEntityId, CancellationToken ct = default) => Task.FromResult(true);
    public Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    public Task<Guid?> IssueStockAsync(StockIssueRequest request, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    public Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default) => Task.FromResult(0m);
    public Task ReverseReceiptAsync(string reference, CancellationToken ct = default) => Task.CompletedTask;
    public Task ReverseIssueAsync(string reference, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyDictionary<Guid, decimal>> GetOnHandAsync(IReadOnlyCollection<Guid> itemIds, Guid? warehouseId, Guid legalEntityId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal>());
    public Task<Guid?> ReturnIssuedStockAsync(string issueReference, Guid itemId, decimal quantity, string returnReference, DateOnly date, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
}
