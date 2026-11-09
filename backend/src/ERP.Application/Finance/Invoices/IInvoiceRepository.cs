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
}
