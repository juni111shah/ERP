using ERP.Application.Abstractions;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class SettlementRepository(IRepository<Settlement> settlements) : RepositoryBase<Settlement>(settlements), ISettlementRepository
{
    public Task<List<Settlement>> ListReceiptsAllocatedToInvoiceAsync(Guid invoiceId, CancellationToken ct)
        => Query()
            .Include(s => s.Allocations)
            .Where(s => s.Direction == InstrumentDirection.Received && s.Allocations.Any(a => a.InvoiceId == invoiceId))
            .OrderBy(s => s.Date)
            .ToListAsync(ct);
}
