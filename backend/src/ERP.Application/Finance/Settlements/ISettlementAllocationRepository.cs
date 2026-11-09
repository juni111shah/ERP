using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for the allocations of receipts and payments. Implemented in ERP.Infrastructure/Repositories/Finance/SettlementAllocationRepository.cs.</summary>
public interface ISettlementAllocationRepository : IRepository<SettlementAllocation>
{
    /// <summary>All allocations of a settlement, read-only.</summary>
    Task<List<SettlementAllocation>> ListForSettlementAsync(Guid settlementId, CancellationToken ct);

    /// <summary>The allocations of a settlement to invoices, read-only.</summary>
    Task<List<SettlementAllocation>> ListInvoiceAllocationsAsync(Guid? settlementId, CancellationToken ct);
}
