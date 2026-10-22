using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

public enum AcquisitionStatus { Draft = 0, Completed = 1, Cancelled = 2 }
public enum FundingType { BankMortgage = 1, DeveloperPlan = 2, Loan = 3, Other = 4 }
public enum InstalmentPayStatus { Scheduled = 0, Paid = 1 }

/// <summary>
/// Buying a property: price, equity, fees and the funding sources that cover the rest. Completing it capitalises the property
/// (Dr investment property, Cr bank and property financing payable) and fixes the financing.
/// </summary>
[DefaultSort("-purchaseDate")]
public class Acquisition : LegalEntityScopedEntity
{
    [Listable] public string? Number { get; set; }
    [Filterable] public Guid PropertyId { get; set; }
    [Searchable] public string SellerName { get; set; } = default!;
    [Filterable, Sortable] public DateOnly PurchaseDate { get; set; }
    [Searchable] public string? ContractReference { get; set; }
    [Sortable] public decimal PurchasePrice { get; set; }
    public decimal DownPayment { get; set; }
    public decimal TransferFees { get; set; }
    public decimal OtherCosts { get; set; }
    /// <summary>Bank account the down payment, fees and costs are paid from.</summary>
    public Guid? PayFromAccountId { get; set; }
    public string? Notes { get; set; }
    public string CurrencyCode { get; set; } = default!;
    [Filterable, Sortable] public AcquisitionStatus Status { get; private set; } = AcquisitionStatus.Draft;
    public DateTime? CompletedAt { get; private set; }
    public Guid? JournalEntryId { get; private set; }

    public decimal TotalCost => PurchasePrice + TransferFees + OtherCosts;
    public decimal CashAtCompletion => DownPayment + TransferFees + OtherCosts;

    public void Complete(Guid journalId, DateTime now)
    {
        if (Status != AcquisitionStatus.Draft) throw new DomainException($"Only a draft acquisition can be completed (this one is {Status}).");
        Status = AcquisitionStatus.Completed; CompletedAt = now; JournalEntryId = journalId;
    }

    public void Cancel()
    {
        if (Status != AcquisitionStatus.Draft) throw new DomainException("Only a draft acquisition can be cancelled.");
        Status = AcquisitionStatus.Cancelled;
    }
}

/// <summary>A loan, mortgage or developer plan that funds part of an acquisition.</summary>
public class FundingSource : LegalEntityScopedEntity
{
    [Filterable] public Guid AcquisitionId { get; set; }
    [Filterable] public FundingType Type { get; set; }
    [Searchable] public string Lender { get; set; } = default!;
    [Sortable] public decimal Principal { get; set; }
    public decimal AnnualRatePercent { get; set; }
    public int TermMonths { get; set; }
    public DateOnly FirstDueDate { get; set; }
    /// <summary>Level monthly payment (annuity); equal principal for a 0% plan.</summary>
    public decimal MonthlyPayment { get; set; }
}

/// <summary>One scheduled payment of a funding source, split into principal and interest.</summary>
public class FundingInstalment : LegalEntityScopedEntity
{
    [Filterable] public Guid FundingSourceId { get; set; }
    public int Sequence { get; set; }
    [Filterable, Sortable] public DateOnly DueDate { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Total { get; set; }
    /// <summary>Principal still owed after this instalment.</summary>
    public decimal BalanceAfter { get; set; }
    [Filterable] public InstalmentPayStatus Status { get; set; }
    public DateOnly? PaidOn { get; set; }
    public Guid? JournalEntryId { get; set; }
}
