using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Portal;
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

    public async Task<Settlement> GetReceiptWithAllocationsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Settlement>(r => r.Id == id && r.Direction == InstrumentDirection.Received).Include(r => r.Allocations))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Settlement> GetPaymentWithAllocationsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Settlement>(p => p.Id == id && p.Direction == InstrumentDirection.Issued).Include(p => p.Allocations))
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<PortalReceiptDto>> ListIssuedReceiptsForPortalAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Settlement>(s => s.PartyId == partyId && s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued))
            .OrderByDescending(s => s.Date).Take(200)
            .Select(s => new PortalReceiptDto(s.Id, s.Number, s.Date, s.Method, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct);

    public Task<List<decimal>> ListUnallocatedReceiptAmountsAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Settlement>(s => s.PartyId == partyId && s.Direction == InstrumentDirection.Received && s.Status == DocumentStatus.Issued && s.Unallocated > 0))
            .Select(s => s.Unallocated).ToListAsync(ct);

    public Task<List<PortalPaymentDto>> ListIssuedPaymentsForPortalAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Settlement>(s => s.PartyId == partyId && s.Direction == InstrumentDirection.Issued && s.Status == DocumentStatus.Issued))
            .OrderByDescending(s => s.Date).Take(200)
            .Select(s => new PortalPaymentDto(s.Id, s.Number, s.Date, s.Method, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct);
}
