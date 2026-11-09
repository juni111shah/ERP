using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.RealEstate.Repositories;

/// <summary>What the real estate features read from customer invoices (rent, termination credits, the tenant portal). Implemented in RealEstateInvoiceRepository.cs.</summary>
public interface IRealEstateInvoiceRepository : IRepository<Invoice>
{
    /// <summary>The invoice with its lines, tracked for changes. Throws when it does not exist.</summary>
    Task<Invoice> FirstWithLinesForUpdateAsync(Guid? id, CancellationToken ct);

    /// <summary>Total and paid of each of the invoices, by id.</summary>
    Task<Dictionary<Guid, InvoiceTotalAndPaid>> MapTotalAndPaidAsync(List<Guid> ids, CancellationToken ct);

    /// <summary>The issued and partly paid invoices and credit notes of the parties.</summary>
    Task<List<OpenInvoiceRow>> ListOpenOfPartiesAsync(List<Guid> partyIds, CancellationToken ct);

    /// <summary>The invoices and credit notes raised from <paramref name="sourceType"/> dated from <paramref name="from"/> to <paramref name="to"/> that are neither drafts nor voided, of one legal entity or (null) all.</summary>
    Task<List<SourceInvoiceRow>> ListPostedOfSourceAsync(string sourceType, DateOnly from, DateOnly to, Guid? legalEntityId, CancellationToken ct);
}

/// <summary>An invoice's total and what was paid on it.</summary>
public sealed record InvoiceTotalAndPaid(Guid Id, decimal Total, decimal Paid);

/// <summary>An open invoice or credit note of a party, with the document it was raised from.</summary>
public sealed record OpenInvoiceRow(Guid PartyId, bool IsCreditNote, decimal Total, decimal Paid, string? SourceType, Guid? SourceId);

/// <summary>An invoice or credit note raised from a source document, with its net and total.</summary>
public sealed record SourceInvoiceRow(Guid Id, Guid? SourceId, bool IsCreditNote, decimal Net, decimal Total);
