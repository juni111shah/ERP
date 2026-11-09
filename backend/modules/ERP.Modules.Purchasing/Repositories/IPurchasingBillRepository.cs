using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Purchasing.Repositories;

/// <summary>The vendor bills as purchasing changes them (returns take amounts off them). Implemented in PurchasingBillRepository.cs.</summary>
public interface IPurchasingBillRepository : IRepository<Bill>
{
    /// <summary>The bill, tracked for changes; null when not found.</summary>
    Task<Bill?> FindForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The bill with its lines, tracked for changes; null when not found.</summary>
    Task<Bill?> FindWithLinesForUpdateAsync(Guid id, CancellationToken ct);
}
