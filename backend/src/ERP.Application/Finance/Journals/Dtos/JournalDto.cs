using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record JournalLineDto(Guid Id, int Seq, Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Description, Guid? PartyId);
public sealed record JournalDto(
    Guid Id, string Number, Guid LegalEntityId, DateOnly Date, string? Memo, string? Reference, string SourceType, Guid? SourceId, string CurrencyCode,
    JournalStatus Status, decimal Amount, Guid? ReversalOfId, DateTime? PostedAt, IReadOnlyList<JournalLineDto> Lines, Guid? ApprovalRequestId = null);

public sealed record JournalSummaryDto(Guid Id, string Number, Guid LegalEntityId, DateOnly Date, string? Memo, string SourceType, JournalStatus Status, decimal Amount, string CurrencyCode)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<JournalEntry, JournalSummaryDto>> Projection =
        j => new JournalSummaryDto(j.Id, j.Number, j.LegalEntityId, j.Date, j.Memo, j.SourceType, j.Status, j.Amount, j.CurrencyCode);
}
