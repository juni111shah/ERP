using System.Linq.Expressions;
using ERP.Application.Abstractions;
using ERP.Application.Common;
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

    public async Task<Settlement> GetReceiptWithAllocationsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Settlement>(x => x.Id == id && x.Direction == InstrumentDirection.Received).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Settlement> GetReceiptWithAllocationsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<Settlement>(x => x.Id == id && x.Direction == InstrumentDirection.Received).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Settlement> GetPaymentWithAllocationsAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Settlement>(s => s.Id == id && s.Direction == InstrumentDirection.Issued).Include(s => s.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Settlement> GetPaymentWithAllocationsForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<Settlement>(x => x.Id == id && x.Direction == InstrumentDirection.Issued).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Settlement> GetWithAllocationsForUpdateAsync(Guid? id, CancellationToken ct)
        => await Tracked(new Specification<Settlement>(x => x.Id == id).Include(x => x.Allocations)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<PagedResult<TDto>> PageByDirectionAsync<TDto>(InstrumentDirection direction, PagedQuery query, Expression<Func<Settlement, TDto>> projection, CancellationToken ct)
        => PageAsync(query, projection, new Specification<Settlement>(s => s.Direction == direction), ct);

    public Task<List<SettlementAmount>> ListNotVoidedAsOfAsync(InstrumentDirection direction, DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => NotVoidedAsOf(direction, asOf, entityId, partyId).Select(s => new SettlementAmount(s.Id, s.PartyId, s.PartyName, s.Amount)).ToListAsync(ct);

    public async Task<Dictionary<Guid, decimal>> AllocatedPerSettlementAsOfAsync(InstrumentDirection direction, DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => (await NotVoidedAsOf(direction, asOf, entityId, partyId).SelectMany(s => s.Allocations).Select(a => new { a.SettlementId, a.Amount }).ToListAsync(ct))
            .GroupBy(a => a.SettlementId).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

    public async Task<Dictionary<Guid, decimal>> AllocatedPerInvoiceAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => (await NotVoidedAsOf(InstrumentDirection.Received, asOf, entityId, partyId).SelectMany(s => s.Allocations).Select(a => new { a.InvoiceId, a.Amount }).ToListAsync(ct))
            .Where(a => a.InvoiceId != null).GroupBy(a => a.InvoiceId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

    public async Task<Dictionary<Guid, decimal>> AllocatedPerBillAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
        => (await NotVoidedAsOf(InstrumentDirection.Issued, asOf, entityId, partyId).SelectMany(s => s.Allocations).Select(a => new { a.BillId, a.Amount }).ToListAsync(ct))
            .Where(a => a.BillId != null).GroupBy(a => a.BillId!.Value).ToDictionary(g => g.Key, g => g.Sum(a => a.Amount));

    public async Task<decimal> StatementTotalBeforeAsync(InstrumentDirection direction, Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => (await OnStatement(direction, partyId, to, entityId).Where(s => s.Date < from).Select(s => s.Amount).ToListAsync(ct)).Sum(); // summed on the client: decimal SUM is not portable

    public Task<List<PartySettlementRow>> ListStatementRowsAsync(InstrumentDirection direction, Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => OnStatement(direction, partyId, to, entityId).Where(s => s.Date >= from)
            .Select(s => new PartySettlementRow(s.Id, s.Number, s.Date, s.PartyName, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct);

    public Task<List<VoidedSettlement>> ListVoidedAfterLockAsync(InstrumentDirection direction, Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<Settlement>(s => s.Direction == direction && s.PartyId == partyId && s.Status == DocumentStatus.Voided && s.VoidPostedOn != null && s.VoidPostedOn <= to && (entityId == null || s.LegalEntityId == entityId)))
            .Select(s => new VoidedSettlement(s.Id, s.Number, s.VoidPostedOn!.Value, s.CurrencyCode, s.Amount, s.Reference)).ToListAsync(ct);

    /// <summary>Receipts (received) or payments (issued) dated on or before the date that were not voided by the end of that day.</summary>
    private IQueryable<Settlement> NotVoidedAsOf(InstrumentDirection direction, DateOnly asOf, Guid? entityId, Guid? partyId)
    {
        var cutoff = asOf.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return Query(new Specification<Settlement>(s => s.Direction == direction && s.Date <= asOf && !(s.VoidedAt != null && s.VoidedAt < cutoff)
            && (s.Status == DocumentStatus.Issued || s.Status == DocumentStatus.Voided)
            && (entityId == null || s.LegalEntityId == entityId) && (partyId == null || s.PartyId == partyId)));
    }

    /// <summary>Receipts (received) or payments (issued) of the party up to a date. Voided ones are left out, unless the void came after the period was locked.</summary>
    private IQueryable<Settlement> OnStatement(InstrumentDirection direction, Guid partyId, DateOnly to, Guid? entityId)
        => Query(new Specification<Settlement>(s => s.Direction == direction && s.PartyId == partyId && s.Date <= to
            && (s.Status == DocumentStatus.Issued || (s.Status == DocumentStatus.Voided && s.VoidPostedOn != null)) && (entityId == null || s.LegalEntityId == entityId)));
}
