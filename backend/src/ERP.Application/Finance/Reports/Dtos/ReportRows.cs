namespace ERP.Application.Finance;

// Rows the receivables and payables reports read from the repositories (never sent to a client as such).

/// <summary>A receipt or payment and its amount.</summary>
public sealed record SettlementAmount(Guid Id, Guid PartyId, string? PartyName, decimal Amount);

/// <summary>A receipt or payment on a party statement.</summary>
public sealed record PartySettlementRow(Guid Id, string Number, DateOnly Date, string? PartyName, string CurrencyCode, decimal Amount, string? Reference);

/// <summary>A receipt or payment voided after its period was locked; it is taken back on <see cref="On"/>.</summary>
public sealed record VoidedSettlement(Guid Id, string Number, DateOnly On, string CurrencyCode, decimal Amount, string? Reference);

/// <summary>An invoice or credit note on a customer statement.</summary>
public sealed record InvoiceStatementRow(Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, string? PartyName, string CurrencyCode, decimal Total);

/// <summary>An invoice or credit note voided after its period was locked; it is taken back on <see cref="On"/>.</summary>
public sealed record VoidedInvoiceRow(Guid Id, string Number, bool IsCreditNote, DateOnly On, string CurrencyCode, decimal Total);

/// <summary>A bill on a vendor statement.</summary>
public sealed record BillStatementRow(Guid Id, string Number, DateOnly BillDate, string? PartyName, string CurrencyCode, decimal Total, string? VendorReference);

/// <summary>A bill voided after its period was locked; it is taken back on <see cref="On"/>.</summary>
public sealed record VoidedBillRow(Guid Id, string Number, DateOnly On, string CurrencyCode, decimal Total, string? VendorReference);

/// <summary>An invoice or credit note that may be open at a date (<see cref="Paid"/> is filled in by the report).</summary>
public sealed record ReceivableOpenItem(
    Guid PartyId, string? PartyName, Guid Id, string Number, bool IsCreditNote, DateOnly InvoiceDate, DateOnly DueDate, decimal Total, decimal Paid,
    Guid? BusinessUnitId = null, string? SourceType = null);

/// <summary>A bill that may be open at a date (<see cref="Paid"/> is filled in by the report).</summary>
public sealed record PayableOpenItem(Guid PartyId, string? PartyName, Guid Id, string Number, DateOnly BillDate, DateOnly DueDate, decimal Total, decimal Paid);

/// <summary>A credit note applied to an invoice.</summary>
public sealed record CreditApplicationAmount(Guid CreditNoteId, Guid InvoiceId, decimal Amount);
