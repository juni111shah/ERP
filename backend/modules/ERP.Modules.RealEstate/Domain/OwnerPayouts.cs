using ERP.Domain.Common;

namespace ERP.Modules.RealEstate.Domain;

/// <summary>How an owner's money leaves the company: a payment from a bank account now, or a vendor bill that accounts payable pays later.</summary>
public enum OwnerPayoutMethod { Payment = 1, Bill = 2 }
public enum OwnerPayoutStatus { Posted = 1, Voided = 2 }

/// <summary>
/// Settles what is held for an owner for a period, from the owner statement of the rent that was credited to the owner payable: Dr owner payable (rent collected),
/// Cr management fee income, Cr maintenance recharged, Cr bank or accounts payable (the net). The figures are kept as they were when it was posted.
/// </summary>
[DefaultSort("-date")]
public class OwnerPayout : LegalEntityScopedEntity, IVoidable
{
    [Listable] public string Number { get; set; } = default!;
    [Filterable] public Guid OwnerPartyId { get; set; }
    [Filterable, Sortable] public DateOnly PeriodFrom { get; set; }
    [Filterable, Sortable] public DateOnly PeriodTo { get; set; }
    [Filterable, Sortable] public DateOnly Date { get; set; }
    [Filterable] public OwnerPayoutMethod Method { get; set; } = OwnerPayoutMethod.Payment;
    [Filterable] public OwnerPayoutStatus Status { get; set; } = OwnerPayoutStatus.Posted;
    public decimal RentCollected { get; set; }
    public decimal ManagementFee { get; set; }
    public decimal MaintenanceCosts { get; set; }
    /// <summary>What the owner receives: rent collected less the fee and the owner's share of maintenance.</summary>
    [Sortable] public decimal Amount { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? BillId { get; set; }
    /// <summary>The payment journal, or on the bill route the journal that takes the fee and maintenance out of the payable (none when both are zero).</summary>
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
}
