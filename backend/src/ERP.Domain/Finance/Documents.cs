using ERP.Domain.Common;

namespace ERP.Domain.Finance;

public enum DocumentStatus { Draft, PendingApproval, Issued, PartlyPaid, Paid, Voided }
public enum SettlementMethod { Cash, BankTransfer, Card, Instrument, Other }

/// <summary>Sales invoice or credit note (core receivables). Schema only in sprint 0; modules link via SourceType/SourceId.</summary>
[DefaultSort("-invoiceDate")]
public class Invoice : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = default!;
    [Filterable]
    public bool IsCreditNote { get; set; }
    [Filterable]
    public Guid PartyId { get; set; }
    /// <summary>Name of the party when the document was issued (documents keep what was printed).</summary>
    [Searchable]
    public string? PartyName { get; set; }
    [Sortable, Filterable]
    public DateOnly InvoiceDate { get; set; }
    [Sortable, Filterable]
    public DateOnly DueDate { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal Net { get; set; }
    public decimal Tax { get; set; }
    [Sortable]
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    [Filterable]
    public DocumentStatus Status { get; set; }
    [Filterable]
    public string? SourceType { get; set; }
    [Filterable]
    public Guid? SourceId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine : Entity
{
    public Guid InvoiceId { get; set; }
    public int Seq { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Net { get; set; }
    public Guid? AccountId { get; set; }
}

/// <summary>Vendor bill (core payables). Schema only in sprint 0.</summary>
[DefaultSort("-billDate")]
public class Bill : LegalEntityScopedEntity, IHasCustomFields
{
    [Searchable, Sortable]
    public string Number { get; set; } = default!;
    [Filterable]
    public Guid PartyId { get; set; }
    [Searchable]
    public string? PartyName { get; set; }
    [Searchable]
    public string? VendorReference { get; set; }
    [Sortable, Filterable]
    public DateOnly BillDate { get; set; }
    [Sortable, Filterable]
    public DateOnly DueDate { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal Net { get; set; }
    public decimal Tax { get; set; }
    [Sortable]
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    [Filterable]
    public DocumentStatus Status { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }
    public List<BillLine> Lines { get; set; } = new();
}

public class BillLine : Entity
{
    public Guid BillId { get; set; }
    public int Seq { get; set; }
    public Guid? ItemId { get; set; }
    public string Description { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public Guid? TaxCodeId { get; set; }
    public decimal TaxAmount { get; set; }
    public bool TaxRecoverable { get; set; } = true;
    public decimal Net { get; set; }
    public Guid? AccountId { get; set; }
}

/// <summary>Money received from (Direction=Received) or paid to (Issued) a party, allocated to invoices or bills. Schema only.</summary>
[DefaultSort("-date")]
public class Settlement : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Number { get; set; } = default!;
    [Filterable]
    public InstrumentDirection Direction { get; set; }
    [Filterable]
    public Guid PartyId { get; set; }
    [Searchable]
    public string? PartyName { get; set; }
    [Sortable, Filterable]
    public DateOnly Date { get; set; }
    public SettlementMethod Method { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? PaymentInstrumentId { get; set; }
    public string CurrencyCode { get; set; } = default!;
    [Sortable]
    public decimal Amount { get; set; }
    public decimal Unallocated { get; set; }
    [Searchable]
    public string? Reference { get; set; }
    [Filterable]
    public DocumentStatus Status { get; set; }
    public Guid? JournalEntryId { get; set; }
    public List<SettlementAllocation> Allocations { get; set; } = new();
}

public class SettlementAllocation : Entity
{
    public Guid SettlementId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? BillId { get; set; }
    public decimal Amount { get; set; }
}

public enum StatementLineStatus { Unmatched, Matched }

/// <summary>One line of a bank statement, matched against a ledger line of the bank account during reconciliation.</summary>
[DefaultSort("-date")]
public class BankStatementLine : LegalEntityScopedEntity
{
    [Filterable]
    public Guid AccountId { get; set; }
    [Sortable, Filterable]
    public DateOnly Date { get; set; }
    [Searchable]
    public string Description { get; set; } = default!;
    [Searchable]
    public string? Reference { get; set; }
    /// <summary>Positive = money in, negative = money out (from the bank's point of view of the account).</summary>
    [Sortable]
    public decimal Amount { get; set; }
    [Filterable]
    public StatementLineStatus Status { get; set; }
    public Guid? MatchedJournalLineId { get; set; }
}
