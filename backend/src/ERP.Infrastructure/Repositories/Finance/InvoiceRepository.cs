using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Finance;
using ERP.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using ERP.Application.Portal;

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

    public Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Invoice>(i => ids.Contains(i.Id))).Select(i => new { i.Id, i.Number }).ToDictionaryAsync(i => i.Id, i => i.Number, ct);

    public Task<List<InvoiceOutstanding>> ListOpenByDueDateAsync(List<Guid> ids, CancellationToken ct)
        => Query(new Specification<Invoice>(i => ids.Contains(i.Id) && !i.IsCreditNote && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid)))
            .OrderBy(i => i.DueDate).Select(i => new InvoiceOutstanding(i.Id, i.Total - i.Paid)).ToListAsync(ct);

    public async Task<decimal> StatementTotalBeforeAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        // Sum on the client: decimal SUM is not portable across the supported databases (same as the trial balance).
        => (await OnStatement(partyId, to, entityId).Where(i => i.InvoiceDate < from).Select(i => new { i.IsCreditNote, i.Total }).ToListAsync(ct)).Sum(i => i.IsCreditNote ? -i.Total : i.Total);

    public Task<List<InvoiceStatementRow>> ListStatementRowsAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct)
        => OnStatement(partyId, to, entityId).Where(i => i.InvoiceDate >= from)
            .Select(i => new InvoiceStatementRow(i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.PartyName, i.CurrencyCode, i.Total)).ToListAsync(ct);

    public Task<List<VoidedInvoiceRow>> ListVoidedAfterLockAsync(Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct)
        => Query(new Specification<Invoice>(i => i.PartyId == partyId && i.Status == DocumentStatus.Voided && i.VoidPostedOn != null && i.VoidPostedOn <= to && (entityId == null || i.LegalEntityId == entityId)))
            .Select(i => new VoidedInvoiceRow(i.Id, i.Number, i.IsCreditNote, i.VoidPostedOn!.Value, i.CurrencyCode, i.Total)).ToListAsync(ct);

    public Task<List<ReceivableOpenItem>> ListPostedAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, Guid? businessUnitId, string? sourceType, IReadOnlyCollection<Guid>? onlyParties, CancellationToken ct)
    {
        var cutoff = asOf.AddDays(1).ToDateTime(TimeOnly.MinValue); // not voided by the end of the day
        return Query(new Specification<Invoice>(i => (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid || i.Status == DocumentStatus.Paid || i.Status == DocumentStatus.Voided)
                && i.InvoiceDate <= asOf && !(i.VoidedAt != null && i.VoidedAt < cutoff)
                && (entityId == null || i.LegalEntityId == entityId) && (partyId == null || i.PartyId == partyId)
                && (businessUnitId == null || i.BusinessUnitId == businessUnitId) && (string.IsNullOrWhiteSpace(sourceType) || i.SourceType == sourceType)
                && (onlyParties == null || onlyParties.Contains(i.PartyId))))
            .Select(i => new ReceivableOpenItem(i.PartyId, i.PartyName, i.Id, i.Number, i.IsCreditNote, i.InvoiceDate, i.DueDate, i.Total, 0m, i.BusinessUnitId, i.SourceType)).ToListAsync(ct);
    }

    /// <summary>The customer's documents on its statement up to a date: posted ones, and voided ones whose void came after the period was locked.</summary>
    private IQueryable<Invoice> OnStatement(Guid partyId, DateOnly to, Guid? entityId) => Query(new Specification<Invoice>(i => i.PartyId == partyId && i.InvoiceDate <= to
        && (i.Status == DocumentStatus.Issued || i.Status == DocumentStatus.PartlyPaid || i.Status == DocumentStatus.Paid || (i.Status == DocumentStatus.Voided && i.VoidPostedOn != null))
        && (entityId == null || i.LegalEntityId == entityId)));

    /// <summary>The non-voided credit notes raised from <paramref name="invoiceId"/>.</summary>
    private IQueryable<Invoice> LinkedCreditNotes(Guid invoiceId, Guid? excludeCreditNoteId) => Query().Where(i =>
        i.IsCreditNote && i.SourceType == InvoiceCreditCaps.LinkType && i.SourceId == invoiceId && i.Id != excludeCreditNoteId && i.Status != DocumentStatus.Voided);

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
}
