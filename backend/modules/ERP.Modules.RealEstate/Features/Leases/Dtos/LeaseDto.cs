using System.Text.Json.Nodes;
using ERP.Modules.RealEstate.Domain;

namespace ERP.Modules.RealEstate.Features;

public sealed record InstalmentDto(Guid Id, int Sequence, DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueDate, decimal Amount, InstalmentStatus Status, Guid? InvoiceId)
{
    public static InstalmentDto From(RentInstalment i) => new(i.Id, i.Sequence, i.PeriodFrom, i.PeriodTo, i.DueDate, i.Amount, i.Status, i.InvoiceId);
}

public sealed record LeaseDto(
    Guid Id, string? Number, Guid UnitId, string UnitNumber, Guid PropertyId, Guid LegalEntityId, Guid LesseePartyId, string TenantName, DateOnly StartDate, DateOnly EndDate,
    decimal AnnualRent, string CurrencyCode, int PaymentFrequency, decimal SecurityDepositAmount, Guid? TaxCodeId, string SuggestedVat, LeaseStatus Status,
    DateTime? ActivatedAt, DateOnly? TerminationDate, string? TerminationReason, Guid? RenewalOfLeaseId, Guid? RenewedAsLeaseId, int GraceDays,
    int NoticePeriodDays, bool HoldingOverFlag, string? Notes, decimal RentForTerm, int? DaysToExpiry, JsonObject CustomFields,
    IReadOnlyList<InstalmentDto> Schedule);

/// <summary>A row of the leases list (spec RE-LSE-12).</summary>
public sealed record LeaseListItem(
    Guid Id, string? Number, Guid UnitId, string UnitNumber, Guid PropertyId, string PropertyName, Guid LesseePartyId, string TenantName,
    DateOnly StartDate, DateOnly EndDate, int? DaysToExpiry, decimal AnnualRent, int PaymentFrequency, decimal SecurityDepositAmount, LeaseStatus Status);

public sealed record TerminationResultDto(LeaseDto Lease, int CancelledInstalments, IReadOnlyList<InstalmentDto> InstalmentsNeedingCreditNote);
public sealed record ExpiryRunResultDto(int Ended, int HeldOver, int ReservationsReleased);
