using ERP.Application.Abstractions;
using ERP.Modules.RealEstate.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RentInstalmentRepository(IRepository<RentInstalment> inner) : RepositoryBase<RentInstalment>(inner), IRentInstalmentRepository
{
    public Task<List<RentInstalment>> ListOfLeaseAsync(Guid leaseId, CancellationToken ct)
        => Query(new Specification<RentInstalment>(i => i.LeaseId == leaseId)).OrderBy(i => i.Sequence).ToListAsync(ct);

    public Task<List<RentInstalment>> ListNotCancelledOfLeaseForUpdateAsync(Guid leaseId, CancellationToken ct)
        => Tracked(new Specification<RentInstalment>(i => i.LeaseId == leaseId && i.Status != InstalmentStatus.Cancelled)).ToListAsync(ct);

    public Task<List<RentInstalment>> ListInvoicedPastDateNotCreditedForUpdateAsync(Guid leaseId, DateOnly date, CancellationToken ct)
        => Tracked(new Specification<RentInstalment>(i => i.LeaseId == leaseId && i.Status == InstalmentStatus.Invoiced && i.CreditNoteId == null && i.InvoiceId != null && i.PeriodTo > date)).ToListAsync(ct);

    public Task<List<RentInstalment>> ListScheduledDueForUpdateAsync(DateOnly asOf, Guid? legalEntityId, CancellationToken ct)
        => Tracked(new Specification<RentInstalment>(i => i.Status == InstalmentStatus.Scheduled && i.DueDate <= asOf && (legalEntityId == null || i.LegalEntityId == legalEntityId)))
            .OrderBy(i => i.DueDate).ThenBy(i => i.Sequence).ToListAsync(ct);

    public Task<RentInstalment?> FindInvoicedByForUpdateAsync(Guid id, Guid invoiceId, CancellationToken ct)
        => Tracked(new Specification<RentInstalment>(x => x.Id == id && x.InvoiceId == invoiceId)).FirstOrDefaultAsync(ct);

    public Task<List<RentInstalment>> ListOfInvoicesAsync(List<Guid> invoiceIds, CancellationToken ct)
        => Query(new Specification<RentInstalment>(i => i.InvoiceId != null && invoiceIds.Contains(i.InvoiceId.Value))).ToListAsync(ct);

    public Task<List<RentInstalment>> ListNotCancelledOfLeasesByDueDateAsync(List<Guid> leaseIds, CancellationToken ct)
        => Query(new Specification<RentInstalment>(i => leaseIds.Contains(i.LeaseId) && i.Status != InstalmentStatus.Cancelled)).OrderBy(i => i.DueDate).ToListAsync(ct);

    public Task<Dictionary<Guid, Guid>> MapLeaseIdsAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<RentInstalment>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.LeaseId }).ToDictionaryAsync(i => i.Id, i => i.LeaseId, ct);
}
