using ERP.Application.Abstractions;
using ERP.Application.Portal;
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

    /// <summary>The number of an invoice, or null when it does not exist.</summary>
    Task<string?> GetNumberAsync(Guid id, CancellationToken ct);

    /// <summary>Invoice numbers by id, for the ids that exist.</summary>
    Task<Dictionary<Guid, string>> GetNumbersAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The latest 200 invoices and credit notes of a portal customer, leaving out drafts, ones pending approval and voided ones.</summary>
    Task<List<PortalInvoiceDto>> ListForPortalAsync(Guid partyId, CancellationToken ct);

    /// <summary>The party's issued or partly paid invoices and credit notes dated on or before <paramref name="asOf"/>.</summary>
    Task<List<InvoiceOpenItem>> ListOpenForPartyAsync(Guid partyId, DateOnly asOf, CancellationToken ct);

    /// <summary>Every issued or partly paid invoice and credit note, of one legal entity or (null) all of them.</summary>
    Task<List<InvoiceOpenBalance>> ListOpenBalancesAsync(Guid? legalEntityId, CancellationToken ct);
}

/// <summary>An open invoice or credit note on a party's statement.</summary>
public sealed record InvoiceOpenItem(string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid);

/// <summary>What is still owed on one open invoice or credit note.</summary>
public sealed record InvoiceOpenBalance(string? PartyName, bool IsCreditNote, decimal Total, decimal Paid, DateOnly DueDate);
