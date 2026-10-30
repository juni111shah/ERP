using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

/// <summary>
/// A tenancy contract. Lifecycle (spec 4.2): Draft -> Active -> Ended | Renewed | Terminated | HoldingOver; a Draft can be Cancelled.
/// The rent schedule is fixed at activation and lives in <see cref="RentInstalment"/> rows.
/// </summary>
[DefaultSort("-startDate")]
public class Lease : LegalEntityScopedEntity, IHasCustomFields
{
    public static readonly int[] PaymentPlans = [1, 2, 4, 6, 12];

    [Listable] public string? Number { get; set; }
    [Filterable] public Guid UnitId { get; set; }
    [Filterable] public Guid PropertyId { get; set; }
    /// <summary>The tenant: a core party.</summary>
    [Filterable] public Guid LesseePartyId { get; set; }
    [Filterable, Sortable] public DateOnly StartDate { get; set; }
    [Filterable, Sortable] public DateOnly EndDate { get; set; }
    /// <summary>Annual rent excluding VAT.</summary>
    [Sortable] public decimal AnnualRent { get; set; }
    public string CurrencyCode { get; set; } = default!;
    /// <summary>Instalments per year: 1, 2, 4, 6 or 12.</summary>
    public int PaymentFrequency { get; set; } = 4;
    public decimal SecurityDepositAmount { get; set; }
    /// <summary>Overrides the VAT treatment derived from the unit usage (core tax code).</summary>
    public Guid? TaxCodeId { get; set; }
    [Filterable, Sortable] public LeaseStatus Status { get; private set; } = LeaseStatus.Draft;
    public DateTime? ActivatedAt { get; private set; }
    public Guid? ActivatedBy { get; private set; }
    public DateOnly? TerminationDate { get; private set; }
    public string? TerminationReason { get; private set; }
    [Filterable] public Guid? RenewalOfLeaseId { get; set; }
    /// <summary>Rent-free days at the start of the term; billing starts after them.</summary>
    public int GraceDays { get; set; }
    public int NoticePeriodDays { get; set; } = 90;
    /// <summary>Tenant still occupies the unit after the end date (the nightly job then holds the lease over instead of ending it).</summary>
    public bool HoldingOverFlag { get; set; }
    public string? Notes { get; set; }
    public string? CustomFields { get; set; }

    public bool IsOccupying => Status is LeaseStatus.Active or LeaseStatus.HoldingOver;

    /// <summary>End date used when none is given: a one-year term (the same term a renewal gets), i.e. start + 1 year - 1 day.</summary>
    public static DateOnly DefaultEndDate(DateOnly start) => start.AddYears(1).AddDays(-1);

    public void Activate(DateTime now, Guid? userId)
    {
        if (Status != LeaseStatus.Draft) throw new DomainException($"Only a draft lease can be activated (this lease is {Status}).");
        Status = LeaseStatus.Active;
        ActivatedAt = now;
        ActivatedBy = userId;
    }

    public void Cancel()
    {
        if (Status != LeaseStatus.Draft) throw new DomainException("Only a draft lease can be cancelled; an active lease must be terminated.");
        Status = LeaseStatus.Cancelled;
    }

    public void Terminate(DateOnly date, string reason)
    {
        if (!IsOccupying) throw new DomainException($"Only an active lease can be terminated (this lease is {Status}).");
        if (date < StartDate || date > EndDate) throw new DomainException("The termination date must fall within the lease term.");
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("A termination reason is required.");
        Status = LeaseStatus.Terminated;
        TerminationDate = date;
        TerminationReason = reason.Trim();
    }

    /// <summary>Daily job: past its end date. Ends the lease, or holds it over while the tenant is still in occupation.</summary>
    public LeaseStatus Expire(DateOnly today)
    {
        if (Status != LeaseStatus.Active) throw new DomainException($"Only an active lease can expire (this lease is {Status}).");
        if (EndDate >= today) throw new DomainException("The lease has not reached its end date.");
        Status = HoldingOverFlag ? LeaseStatus.HoldingOver : LeaseStatus.Ended;
        return Status;
    }

    /// <summary>HoldingOver -> Ended once the move-out is confirmed.</summary>
    public void ConfirmMoveOut()
    {
        if (Status != LeaseStatus.HoldingOver) throw new DomainException("Only a lease that is holding over can be closed by move-out.");
        Status = LeaseStatus.Ended;
        HoldingOverFlag = false;
    }

    /// <summary>The renewal lease was activated.</summary>
    public void MarkRenewed()
    {
        if (!IsOccupying) throw new DomainException($"Only an active lease can be renewed (this lease is {Status}).");
        Status = LeaseStatus.Renewed;
    }
}

/// <summary>One row of the rent schedule. Amount is excluding VAT; the rounding remainder sits on the last row.</summary>
public class RentInstalment : LegalEntityScopedEntity
{
    public Guid LeaseId { get; set; }
    public int Sequence { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public InstalmentStatus Status { get; set; } = InstalmentStatus.Scheduled;
    /// <summary>Set by the billing run (Finance sprint) when the instalment is invoiced.</summary>
    public Guid? InvoiceId { get; set; }
    /// <summary>Credit note raised when the lease was terminated early and this instalment had been invoiced beyond the termination date.</summary>
    public Guid? CreditNoteId { get; set; }
}

public enum DepositKind { Received = 1, Damages = 2, Arrears = 3, Refund = 4, TransferOut = 5, TransferIn = 6 }

/// <summary>Money movements on a lease's security deposit: received from the tenant, then applied to damages or arrears, or refunded.</summary>
public class DepositTransaction : LegalEntityScopedEntity
{
    [Filterable] public Guid LeaseId { get; set; }
    [Filterable, Sortable] public DateOnly Date { get; set; }
    [Filterable] public DepositKind Kind { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
}
