using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record BillLineDto(Guid Id, int Seq, string Description, decimal Quantity, decimal Price, Guid? TaxCodeId, decimal TaxAmount, bool TaxRecoverable, decimal Net, Guid? AccountId);

/// <summary>A vendor bill with its lines (the detail view).</summary>
public sealed record BillDto(
    Guid Id, string Number, Guid LegalEntityId, Guid PartyId, string? PartyName, string? VendorReference, DateOnly BillDate, DateOnly DueDate, string CurrencyCode, decimal Net, decimal Tax,
    decimal Total, decimal Paid, DocumentStatus Status, Guid? ApprovalRequestId, Guid? JournalEntryId, string? Notes, IReadOnlyList<BillLineDto> Lines,
    string? VoidReason = null, DateTime? VoidedAt = null, Guid? BusinessUnitId = null)
{
    public decimal Outstanding => Total - Paid;

    public static BillDto From(Bill b) => new(
        b.Id, b.Number, b.LegalEntityId, b.PartyId, b.PartyName, b.VendorReference, b.BillDate, b.DueDate, b.CurrencyCode, b.Net, b.Tax, b.Total, b.Paid, b.Status, b.ApprovalRequestId, b.JournalEntryId, b.Notes,
        b.Lines.OrderBy(l => l.Seq).Select(l => new BillLineDto(l.Id, l.Seq, l.Description, l.Quantity, l.Price, l.TaxCodeId, l.TaxAmount, l.TaxRecoverable, l.Net, l.AccountId)).ToList(),
        b.VoidReason, b.VoidedAt, b.BusinessUnitId);
}

/// <summary>One row of the bill list.</summary>
public sealed record BillSummaryDto(Guid Id, string Number, Guid PartyId, string? PartyName, string? VendorReference, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid, DocumentStatus Status, string CurrencyCode)
{
    public decimal Outstanding => Total - Paid;

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<Bill, BillSummaryDto>> Projection =
        b => new BillSummaryDto(b.Id, b.Number, b.PartyId, b.PartyName, b.VendorReference, b.BillDate, b.DueDate, b.Total, b.Paid, b.Status, b.CurrencyCode);
}
