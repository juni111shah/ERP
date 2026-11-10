using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class SettlementAllocationRepository(IRepository<SettlementAllocation> allocations)
    : RepositoryBase<SettlementAllocation>(allocations), ISettlementAllocationRepository
{
    public Task<List<SettlementAllocation>> ListForSettlementAsync(Guid settlementId, CancellationToken ct)
        => Query(new Specification<SettlementAllocation>(a => a.SettlementId == settlementId)).ToListAsync(ct);

    public Task<List<SettlementAllocation>> ListInvoiceAllocationsAsync(Guid? settlementId, CancellationToken ct)
        => Query(new Specification<SettlementAllocation>(a => a.SettlementId == settlementId && a.InvoiceId != null)).ToListAsync(ct);
}
