using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>A cheque in the register with the allocations of its receipt (the detail view).</summary>
public sealed record ChequeDto(
    Guid Id, string Number, Guid LegalEntityId, InstrumentKind Kind, InstrumentDirection Direction, string InstrumentNo, string? BankName, decimal Amount, string CurrencyCode, DateOnly InstrumentDate,
    DateOnly? ReceivedOn, Guid? PartyId, string? PartyName, InstrumentStatus Status, Guid? BankAccountId, Guid? SettlementId, DateOnly? DepositedOn, DateOnly? ClearedOn, string? BounceReason,
    Guid? ReplacesInstrumentId, string? Notes, IReadOnlyList<AllocationDto> Allocations,
    DateOnly? BouncedOn = null, DateOnly? FollowUpOn = null, string? FollowUpNote = null, Guid? ReplacedByChequeId = null);

/// <summary>Totals of the cheques received: in custody, matured, due in the next 7 / 30 days, deposited, and how many bounced.</summary>
public sealed record ChequeSummaryDto(decimal InCustodyTotal, int InCustodyCount, decimal MaturedTotal, int MaturedCount, decimal Next7DaysTotal, decimal Next30DaysTotal, decimal DepositedTotal, int BouncedCount);

/// <summary>One row of the cheque register list.</summary>
public sealed record ChequeSummaryRow(
    Guid Id, string Number, InstrumentKind Kind, InstrumentDirection Direction, string InstrumentNo, string? BankName, Guid? PartyId, string? PartyName, decimal Amount, DateOnly InstrumentDate,
    InstrumentStatus Status, string CurrencyCode, DateOnly? BouncedOn = null, DateOnly? FollowUpOn = null)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<PaymentInstrument, ChequeSummaryRow>> Projection =
        c => new ChequeSummaryRow(c.Id, c.Number, c.Kind, c.Direction, c.InstrumentNo, c.BankName, c.PartyId, c.PartyName, c.Amount, c.InstrumentDate, c.Status, c.CurrencyCode, c.BouncedOn, c.FollowUpOn);
}
