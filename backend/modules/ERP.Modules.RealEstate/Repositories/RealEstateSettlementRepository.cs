using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstateSettlementRepository(IRepository<Settlement> inner) : RepositoryBase<Settlement>(inner), IRealEstateSettlementRepository
{
    public async Task<List<ReceiptAllocation>> ListReceivedAllocationsAsync(DateOnly from, DateOnly to, CancellationToken ct)
        => (await Query(new Specification<Settlement>(s => s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Date >= from && s.Date <= to))
            .SelectMany(s => s.Allocations, (s, a) => new { a.InvoiceId, a.Amount }).ToListAsync(ct))
            .Select(a => new ReceiptAllocation(a.InvoiceId, a.Amount)).ToList();
}
