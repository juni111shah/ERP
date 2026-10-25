using ERP.Application.Abstractions;

namespace ERP.Infrastructure.Services;

public sealed class NullStockPostingService : IStockPostingService
{
    public Task<bool> IsInventoryEnabledAsync(CancellationToken ct = default) => Task.FromResult(false);
    public Task<Guid?> ReceiveStockAsync(StockReceiveRequest request, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    public Task<Guid?> IssueStockAsync(StockIssueRequest request, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    public Task<decimal> GetAverageCostAsync(Guid itemId, Guid? warehouseId = null, CancellationToken ct = default) => Task.FromResult(0m);
}
