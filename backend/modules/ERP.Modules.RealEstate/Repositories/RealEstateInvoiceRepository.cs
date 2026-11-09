using ERP.Application.Abstractions;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.RealEstate.Repositories;

internal sealed class RealEstateInvoiceRepository(IRepository<Invoice> inner) : RepositoryBase<Invoice>(inner), IRealEstateInvoiceRepository
{
    public Task<Invoice> FirstWithLinesForUpdateAsync(Guid? id, CancellationToken ct)
        => Tracked(new Specification<Invoice>(x => x.Id == id).Include(x => x.Lines)).FirstAsync(ct);

    public Task<Dictionary<Guid, InvoiceTotalAndPaid>> MapTotalAndPaidAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Invoice>(v => ids.Contains(v.Id))).Select(v => new InvoiceTotalAndPaid(v.Id, v.Total, v.Paid)).ToDictionaryAsync(v => v.Id, ct);

    public Task<List<OpenInvoiceRow>> ListOpenOfPartiesAsync(List<Guid> partyIds, CancellationToken ct)
        => Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && partyIds.Contains(i.PartyId)))
            .Select(i => new OpenInvoiceRow(i.PartyId, i.IsCreditNote, i.Total, i.Paid, i.SourceType, i.SourceId)).ToListAsync(ct);

    public Task<List<SourceInvoiceRow>> ListPostedOfSourceAsync(string sourceType, DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Invoice>(i => i.SourceType == sourceType && i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.Voided && (legalEntityId == null || i.LegalEntityId == legalEntityId)))
            .Select(i => new SourceInvoiceRow(i.Id, i.SourceId, i.IsCreditNote, i.Net, i.Total)).ToListAsync(ct);
}
