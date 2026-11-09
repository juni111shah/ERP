using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Application.Portal;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Finance;

internal sealed class InvoiceRepository(IRepository<Invoice> invoices) : RepositoryBase<Invoice>(invoices), IInvoiceRepository
{
    public async Task<Invoice> GetWithLinesAsync(Guid id, CancellationToken ct)
        => await Query().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException();

    public async Task<Invoice> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct)
        => await Tracked().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException();

    public Task<List<InvoiceLine>> GetLinesAsync(Guid invoiceId, CancellationToken ct)
        => Query().Where(i => i.Id == invoiceId).SelectMany(i => i.Lines).ToListAsync(ct);

    public Task<DocumentStatus?> GetStatusAsync(Guid id, CancellationToken ct)
        => Query().Where(i => i.Id == id).Select(i => (DocumentStatus?)i.Status).FirstOrDefaultAsync(ct);

    public Task<decimal?> GetTotalAsync(Guid id, CancellationToken ct)
        => Query().Where(i => i.Id == id).Select(i => (decimal?)i.Total).FirstOrDefaultAsync(ct);

    public async Task<decimal> TotalCreditedAsync(Guid invoiceId, Guid? excludeCreditNoteId, CancellationToken ct)
    {
        var totals = await LinkedCreditNotes(invoiceId, excludeCreditNoteId).Select(i => i.Total).ToListAsync(ct);
        return totals.Sum(); // summed in memory: a handful of rows, and SQLite (tests) cannot SUM decimals
    }

    public async Task<Dictionary<Guid, decimal>> CreditedQuantityPerLineAsync(Guid invoiceId, Guid? excludeCreditNoteId, CancellationToken ct)
    {
        var notes = await LinkedCreditNotes(invoiceId, excludeCreditNoteId).Include(i => i.Lines).ToListAsync(ct);
        return notes.SelectMany(n => n.Lines)
            .Where(l => l.SourceLineId != null)
            .GroupBy(l => l.SourceLineId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
    }

    public Task<string?> GetNumberAsync(Guid id, CancellationToken ct)
        => Query(new Specification<Invoice>(i => i.Id == id)).Select(i => i.Number).FirstOrDefaultAsync(ct);

    public Task<Dictionary<Guid, string>> GetNumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Invoice>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Number }).ToDictionaryAsync(i => i.Id, i => i.Number, ct);

    public Task<List<PortalInvoiceDto>> ListForPortalAsync(Guid partyId, CancellationToken ct)
        => Query(new Specification<Invoice>(i => i.PartyId == partyId && i.Status != DocumentStatus.Draft && i.Status != DocumentStatus.PendingApproval && i.Status != DocumentStatus.Voided))
            .OrderByDescending(i => i.InvoiceDate).Take(200)
            .Select(i => new PortalInvoiceDto(i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.Total, i.Paid, i.Total - i.Paid, i.Status)).ToListAsync(ct);

    public Task<List<InvoiceOpenItem>> ListOpenForPartyAsync(Guid partyId, DateOnly asOf, CancellationToken ct)
        => Query(new Specification<Invoice>(i => i.PartyId == partyId && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && i.InvoiceDate <= asOf))
            .Select(i => new InvoiceOpenItem(i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.Total, i.Paid)).ToListAsync(ct);

    public Task<List<InvoiceOpenBalance>> ListOpenBalancesAsync(Guid? legalEntityId, CancellationToken ct)
        => Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid) && (legalEntityId == null || i.LegalEntityId == legalEntityId)))
            .Select(i => new InvoiceOpenBalance(i.PartyName, i.IsCreditNote, i.Total, i.Paid, i.DueDate)).ToListAsync(ct);

    /// <summary>The non-voided credit notes raised from <paramref name="invoiceId"/>.</summary>
    private IQueryable<Invoice> LinkedCreditNotes(Guid invoiceId, Guid? excludeCreditNoteId) => Query().Where(i =>
        i.IsCreditNote && i.SourceType == InvoiceCreditCaps.LinkType && i.SourceId == invoiceId && i.Id != excludeCreditNoteId && i.Status != DocumentStatus.Voided);
}
