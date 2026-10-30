using ERP.Domain.Common;

namespace ERP.Domain.Finance;

[DefaultSort("code")]
public class Currency : Entity
{
    [Listable]
    public string Code { get; set; } = default!; // ISO 4217
    [Listable]
    public string Name { get; set; } = default!;
    public string? Symbol { get; set; }
    public int Decimals { get; set; } = 2;
}

public class ExchangeRate : OrgEntity
{
    public string FromCurrency { get; set; } = default!;
    public string ToCurrency { get; set; } = default!;
    public DateOnly RateDate { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>Tax regime kinds; rates live in data (no hard-coded country rules).</summary>
public enum TaxKind { Vat, Gst, SalesTax, Withholding, Excise, Other }

[DefaultSort("code")]
public class TaxCode : OrgEntity
{
    public Guid? LegalEntityId { get; set; }
    [Listable]
    public string Code { get; set; } = default!;
    [Listable]
    public string Name { get; set; } = default!;
    [Filterable]
    public TaxKind Kind { get; set; }
    [Sortable]
    public decimal Rate { get; set; }
    public bool IsRecoverable { get; set; } = true;
    public bool IsDefault { get; set; }
    [Filterable]
    public bool IsActive { get; set; } = true;
}

public enum AccountType { Asset, Liability, Equity, Income, Expense }

[DefaultSort("code")]
public class Account : OrgEntity, IHasCustomFields
{
    [Listable]
    public string Code { get; set; } = default!;
    [Listable]
    public string Name { get; set; } = default!;
    [Filterable, Sortable]
    public AccountType Type { get; set; }
    [Filterable]
    public string? ReportingGroup { get; set; }
    public Guid? ParentId { get; set; }
    public bool IsPostable { get; set; } = true;
    public bool IsSystem { get; set; }
    [Filterable]
    public bool IsBank { get; set; }
    [Filterable]
    public bool IsActive { get; set; } = true;
    /// <summary>Optional system role for posting rules (e.g. "ar.control", "tax.output").</summary>
    public string? SystemRole { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CustomFields { get; set; }
}

public enum JournalStatus { Draft, Posted, Reversed }

[DefaultSort("-date")]
public class JournalEntry : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Number { get; set; } = default!;
    [Sortable, Filterable]
    public DateOnly Date { get; set; }
    [Searchable]
    public string? Memo { get; set; }
    [Searchable]
    public string? Reference { get; set; }
    [Filterable]
    public string SourceType { get; set; } = "manual";
    [Filterable]
    public Guid? SourceId { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public decimal ExchangeRate { get; set; } = 1m;
    [Filterable]
    public JournalStatus Status { get; set; } = JournalStatus.Draft;
    /// <summary>Total debit, kept in step with the lines so lists can show it without summing in SQL.</summary>
    [Sortable]
    public decimal Amount { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public Guid? ReversalOfId { get; set; }
    public List<JournalLine> Lines { get; set; } = new();

    public decimal TotalDebit => Lines.Sum(l => l.Debit);
    public decimal TotalCredit => Lines.Sum(l => l.Credit);

    /// <summary>Double-entry invariants: balanced, each line one-sided and positive, at least two lines, not in a locked period.</summary>
    public void EnsureCanPost(DateOnly? booksLockedTo)
    {
        if (Status != JournalStatus.Draft) throw new DomainException("Only draft entries can be posted.");
        if (Lines.Count < 2) throw new DomainException("A journal needs at least two lines.");
        foreach (var l in Lines)
        {
            if (l.Debit < 0 || l.Credit < 0) throw new DomainException("Amounts cannot be negative.");
            if ((l.Debit > 0) == (l.Credit > 0)) throw new DomainException("Each line must have either a debit or a credit.");
        }
        if (TotalDebit != TotalCredit) throw new DomainException($"Journal is not balanced (Dr {TotalDebit} / Cr {TotalCredit}).");
        if (booksLockedTo is { } lockDate && Date <= lockDate) throw new DomainException($"Books are locked to {lockDate:yyyy-MM-dd}.");
    }

    public void Recalculate() => Amount = TotalDebit;

    public void Post(Guid userId, DateTime now, DateOnly? booksLockedTo)
    {
        EnsureCanPost(booksLockedTo);
        Status = JournalStatus.Posted;
        PostedAt = now;
        PostedBy = userId;
    }
}

public class JournalLine : Entity
{
    public Guid JournalEntryId { get; set; }
    public int Seq { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
    public Guid? PartyId { get; set; }
    public Guid? BusinessUnitId { get; set; }
    /// <summary>Free analytic dimensions (JSON: {"project":"...","costCenter":"..."}); modules add their own keys.</summary>
    public string? Dimensions { get; set; }
}

public enum InstrumentKind { Cheque, PostDatedCheque, BankDraft, PromissoryNote }
public enum InstrumentDirection { Received, Issued }
public enum InstrumentStatus { InCustody, Deposited, Cleared, Bounced, Replaced, Cancelled, Issued }

/// <summary>Core finance payment instrument (cheques / PDCs). Permissions: finance.cheques.read/write. Schema-only in sprint 0.</summary>
[DefaultSort("instrumentDate")]
public class PaymentInstrument : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Number { get; set; } = default!;
    [Filterable]
    public InstrumentKind Kind { get; set; }
    [Filterable]
    public InstrumentDirection Direction { get; set; }
    [Searchable]
    public string InstrumentNo { get; set; } = default!;
    [Searchable]
    public string? BankName { get; set; }
    [Sortable]
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    /// <summary>The date written on the cheque: a post-dated cheque matures on this date.</summary>
    [Sortable, Filterable]
    public DateOnly InstrumentDate { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    [Filterable]
    public Guid? PartyId { get; set; }
    [Searchable]
    public string? PartyName { get; set; }
    [Filterable]
    public InstrumentStatus Status { get; set; }
    /// <summary>Bank account the cheque is deposited to (received) or drawn on (issued).</summary>
    public Guid? BankAccountId { get; set; }
    public Guid? SettlementId { get; set; }
    public DateOnly? DepositedOn { get; set; }
    public DateOnly? ClearedOn { get; set; }
    public string? BounceReason { get; set; }
    /// <summary>The date the bank returned the cheque.</summary>
    public DateOnly? BouncedOn { get; set; }
    /// <summary>When someone last chased the customer about a bounced cheque, and what was said.</summary>
    public DateOnly? FollowUpOn { get; set; }
    public string? FollowUpNote { get; set; }
    public Guid? ReplacesInstrumentId { get; set; }
    /// <summary>Generic link to the business document it settles (module-defined type + id).</summary>
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>An annual budget of one legal entity: planned income/expense per account and month. Compared with the ledger by the budget-vs-actual report.</summary>
[DefaultSort("-fiscalYear")]
public class Budget : LegalEntityScopedEntity
{
    [Searchable, Sortable]
    public string Name { get; set; } = default!;
    [Filterable, Sortable]
    public int FiscalYear { get; set; }
    [Filterable]
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<BudgetLine> Lines { get; set; } = new();
}

public class BudgetLine : Entity
{
    public Guid BudgetId { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>1-12 within the budget's fiscal year (calendar months).</summary>
    public int Month { get; set; }
    public decimal Amount { get; set; }
}
