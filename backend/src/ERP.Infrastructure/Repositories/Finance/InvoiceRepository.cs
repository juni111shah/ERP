using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
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

    /// <summary>The non-voided credit notes raised from <paramref name="invoiceId"/>.</summary>
    private IQueryable<Invoice> LinkedCreditNotes(Guid invoiceId, Guid? excludeCreditNoteId) => Query().Where(i =>
        i.IsCreditNote && i.SourceType == InvoiceCreditCaps.LinkType && i.SourceId == invoiceId && i.Id != excludeCreditNoteId && i.Status != DocumentStatus.Voided);
}
