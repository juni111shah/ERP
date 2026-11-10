using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record InvoiceLineDto(Guid Id, int Seq, Guid? ItemId, string Description, decimal Quantity, decimal Price, Guid? TaxCodeId, decimal TaxAmount, decimal Net, Guid? AccountId);

/// <summary>An invoice or credit note with its lines (the detail view).</summary>
public sealed record InvoiceDto(
    Guid Id, string Number, bool IsCreditNote, Guid LegalEntityId, Guid PartyId, string? PartyName, DateOnly InvoiceDate, DateOnly DueDate, string CurrencyCode,
    decimal Net, decimal Tax, decimal Total, decimal Paid, DocumentStatus Status, string? SourceType, Guid? SourceId, Guid? JournalEntryId, string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines, Guid? CreditNoteForId = null, string? VoidReason = null, DateTime? VoidedAt = null, IReadOnlyList<string>? Warnings = null)
{
    public decimal Outstanding => Total - Paid;

    public static InvoiceDto From(Invoice i) => new(
        i.Id, i.Number, i.IsCreditNote, i.LegalEntityId, i.PartyId, i.PartyName, i.InvoiceDate, i.DueDate, i.CurrencyCode, i.Net, i.Tax, i.Total, i.Paid, i.Status,
        i.SourceType, i.SourceId, i.JournalEntryId, i.Notes,
        i.Lines.OrderBy(l => l.Seq).Select(l => new InvoiceLineDto(l.Id, l.Seq, l.ItemId, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.Net, l.AccountId)).ToList(),
        i.CreditNoteForId, i.VoidReason, i.VoidedAt);
}

/// <summary>One row of the invoice list.</summary>
public sealed record InvoiceSummaryDto(
    Guid Id, string Number, bool IsCreditNote, Guid PartyId, string? PartyName, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid, DocumentStatus Status,
    string CurrencyCode, decimal Net = 0, decimal Tax = 0, string? SourceType = null)
{
    public decimal Outstanding => Total - Paid;

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Invoice, InvoiceSummaryDto>> Projection = i => new InvoiceSummaryDto(
        i.Id, i.Number, i.IsCreditNote, i.PartyId, i.PartyName, i.InvoiceDate, i.DueDate, i.Total, i.Paid, i.Status, i.CurrencyCode, i.Net, i.Tax, i.SourceType);
}

/// <summary>A customer receipt allocated to one invoice (read-only; for the invoice detail view).</summary>
public sealed record InvoiceAllocationDto(Guid SettlementId, string Number, DateOnly Date, SettlementMethod Method, string? Reference, string CurrencyCode, decimal Amount, DocumentStatus Status);
