using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for invoices and credit notes. Implemented in ERP.Infrastructure/Repositories/Finance/InvoiceRepository.cs.</summary>
public interface IInvoiceRepository : IRepository<Invoice>
{
    /// <summary>The invoice with its lines, read-only. Throws NotFoundException.</summary>
    Task<Invoice> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The invoice with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<Invoice> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The lines of an invoice, read-only.</summary>
    Task<List<InvoiceLine>> GetLinesAsync(Guid invoiceId, CancellationToken ct);

    Task<DocumentStatus?> GetStatusAsync(Guid id, CancellationToken ct);

    Task<decimal?> GetTotalAsync(Guid id, CancellationToken ct);

    /// <summary>The total of the invoice's non-voided linked credit notes, leaving out <paramref name="excludeCreditNoteId"/>.</summary>
    Task<decimal> TotalCreditedAsync(Guid invoiceId, Guid? excludeCreditNoteId, CancellationToken ct);

    /// <summary>Quantity already credited per invoice line by the invoice's non-voided linked credit notes, leaving out <paramref name="excludeCreditNoteId"/>.</summary>
    Task<Dictionary<Guid, decimal>> CreditedQuantityPerLineAsync(Guid invoiceId, Guid? excludeCreditNoteId, CancellationToken ct);

    /// <summary>The number of each of the invoices, by id.</summary>
    Task<Dictionary<Guid, string>> NumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>Those of the invoices (not credit notes) that are open, with what is outstanding, earliest due first.</summary>
    Task<List<InvoiceOutstanding>> ListOpenByDueDateAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The signed total (credit notes negative) of the customer's statement documents dated before <paramref name="from"/>.</summary>
    Task<decimal> StatementTotalBeforeAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The customer's statement documents dated from <paramref name="from"/> to <paramref name="to"/>: posted ones, and voided ones whose void came after the period was locked.</summary>
    Task<List<InvoiceStatementRow>> ListStatementRowsAsync(Guid partyId, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The customer's documents voided after their period was locked, the void posted on or before <paramref name="to"/>.</summary>
    Task<List<VoidedInvoiceRow>> ListVoidedAfterLockAsync(Guid partyId, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>Invoices and credit notes dated on or before <paramref name="asOf"/> that were posted and not voided by the end of that day (<c>Paid</c> left at 0).</summary>
    Task<List<ReceivableOpenItem>> ListPostedAsOfAsync(DateOnly asOf, Guid? entityId, Guid? partyId, Guid? businessUnitId, string? sourceType, IReadOnlyCollection<Guid>? onlyParties, CancellationToken ct);
}
