using System.Linq.Expressions;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

/// <summary>Method Payment needs BankAccountId; method Bill raises a vendor bill to the owner (DueDate is optional) that accounts payable pays later.</summary>
public sealed record OwnerPayoutInput(Guid LegalEntityId, Guid OwnerPartyId, DateOnly From, DateOnly To, DateOnly Date, OwnerPayoutMethod Method = OwnerPayoutMethod.Payment, Guid? BankAccountId = null, DateOnly? DueDate = null, string? Notes = null);

public sealed record OwnerPayoutDto(
    Guid Id, string Number, Guid LegalEntityId, Guid OwnerPartyId, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly Date, OwnerPayoutMethod Method, OwnerPayoutStatus Status,
    decimal RentCollected, decimal ManagementFee, decimal MaintenanceCosts, decimal Amount, Guid? BankAccountId, Guid? BillId, Guid? JournalEntryId, string? Notes, string? VoidReason, DateTime? VoidedAt)
{
    public static OwnerPayoutDto From(OwnerPayout p) => new(p.Id, p.Number, p.LegalEntityId, p.OwnerPartyId, p.PeriodFrom, p.PeriodTo, p.Date, p.Method, p.Status, p.RentCollected, p.ManagementFee, p.MaintenanceCosts, p.Amount, p.BankAccountId, p.BillId, p.JournalEntryId, p.Notes, p.VoidReason, p.VoidedAt);

    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<OwnerPayout, OwnerPayoutDto>> Projection = p => new OwnerPayoutDto(p.Id, p.Number, p.LegalEntityId, p.OwnerPartyId, p.PeriodFrom, p.PeriodTo, p.Date, p.Method, p.Status, p.RentCollected, p.ManagementFee, p.MaintenanceCosts, p.Amount, p.BankAccountId, p.BillId, p.JournalEntryId, p.Notes, p.VoidReason, p.VoidedAt);
}
