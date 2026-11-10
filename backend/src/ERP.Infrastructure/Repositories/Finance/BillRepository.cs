using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Portal;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class BillRepository(IRepository<Bill> bills) : RepositoryBase<Bill>(bills), IBillRepository
{
    public async Task<Bill> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query(new Specification<Bill>(b => b.Id == id).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public async Task<Bill> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked(new Specification<Bill>(b => b.Id == id).Include(b => b.Lines)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException();

    public Task<List<Bill>> ListByIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Bill>(b => ids.Contains(b.Id))).ToListAsync(ct);

    public Task<List<Bill>> ListByIdsForUpdateAsync(List<Guid> ids, CancellationToken ct)
        => Tracked(new Specification<Bill>(b => ids.Contains(b.Id))).ToListAsync(ct);

    public Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Bill>(b => ids.Contains(b.Id))).Select(b => new { b.Id, b.Number }).ToDictionaryAsync(b => b.Id, b => b.Number, ct);

    public async Task<decimal> StatementTotalBeforeAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => (await OnStatement(partyId, to, entityId).Where(b => b.BillDate < from).Select(b => b.Total).ToListAsync(ct)).Sum(); // summed on the client: decimal SUM is not portable

    public Task<List<BillStatementRow>> ListStatementRowsAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => OnStatement(partyId, to, entityId).Where(b => b.BillDate >= from)
            .Select(b => new BillStatementRow(b.Id, b.Number, b.BillDate, b.PartyName, b.CurrencyCode, b.Total, b.VendorReference)).ToListAsync(ct);

    public Task<List<VoidedBillRow>> ListVoidedAfterLockAsync(Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.PartyId == partyId && b.Status == DocumentStatus.Voided && b.VoidPostedOn != null && b.VoidPostedOn <= to && (entityId == null || b.LegalEntityId == entityId)))
            .Select(b => new VoidedBillRow(b.Id, b.Number, b.VoidPostedOn!.Value, b.CurrencyCode, b.Total, b.VendorReference)).ToListAsync(ct);

    public Task<List<PayableOpenItem>> ListPostedAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, CancellationToken ct)
    {
        var cutoff = asOf.AddDays(1).ToDateTime(TimeOnly.MinValue); // not voided by the end of the day
        return Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid || b.Status == DocumentStatus.Voided)
                && b.BillDate <= asOf && !(b.VoidedAt != null && b.VoidedAt < cutoff)
                && (entityId == null || b.LegalEntityId == entityId) && (partyId == null || b.PartyId == partyId)))
            .Select(b => new PayableOpenItem(b.PartyId, b.PartyName, b.Id, b.Number, b.BillDate, b.DueDate, b.Total, 0m)).ToListAsync(ct);
    }

    /// <summary>The vendor's bills on its statement up to a date: posted ones, and voided ones whose void came after the period was locked.</summary>
    private IQueryable<Bill> OnStatement(Guid partyId, DateOnly to, Guid? entityId) => Query(new Specification<Bill>(b => b.PartyId == partyId && b.BillDate <= to
        && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid || b.Status == DocumentStatus.Paid || (b.Status == DocumentStatus.Voided && b.VoidPostedOn != null))
        && (entityId == null || b.LegalEntityId == entityId)));

    public Task<Dictionary<Guid, string>> GetNumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Bill>(b => ids.Contains(b.Id))).Select(b => new { b.Id, b.Number }).ToDictionaryAsync(b => b.Id, b => b.Number, ct);

    public Task<List<PortalBillDto>> ListForPortalAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.PartyId == partyId && b.Status != DocumentStatus.Draft && b.Status != DocumentStatus.PendingApproval && b.Status != DocumentStatus.Voided))
            .OrderByDescending(b => b.BillDate).Take(200)
            .Select(b => new PortalBillDto(b.Id, b.Number, b.VendorReference, b.BillDate, b.DueDate, b.CurrencyCode, b.Total, b.Paid, b.Total - b.Paid, b.Status)).ToListAsync(ct);

    public Task<List<BillOpenItem>> ListOpenForPartyAsync(Guid partyId, DateOnly asOf, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.PartyId == partyId && (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && b.BillDate <= asOf))
            .Select(b => new BillOpenItem(b.Number, b.BillDate, b.DueDate, b.Total, b.Paid)).ToListAsync(ct);

    public Task<List<BillOpenBalance>> ListOpenBalancesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Bill>(b => (b.Status == DocumentStatus.Issued || b.Status == DocumentStatus.PartlyPaid) && (legalEntityId == null || b.LegalEntityId == legalEntityId)))
            .Select(b => new BillOpenBalance(b.PartyName, b.Total, b.Paid, b.DueDate)).ToListAsync(ct);

    public Task<Bill?> FindWithLinesInEntityAsync(Guid id, Guid legalEntityId, CancellationToken ct)
        => Query(new Specification<Bill>(b => b.Id == id && b.LegalEntityId == legalEntityId).Include(b => b.Lines)).FirstOrDefaultAsync(ct);
}
