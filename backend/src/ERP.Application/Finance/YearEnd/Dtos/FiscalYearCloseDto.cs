using System.Linq.Expressions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

public sealed record FiscalYearCloseDto(
    Guid Id, Guid LegalEntityId, int FiscalYear, DateOnly YearStart, DateOnly YearEnd, Guid? JournalEntryId, decimal NetResult, DateOnly? PreviousBooksLockedTo,
    DateTime ClosedAt, Guid? ClosedBy, DateTime? ReopenedAt, Guid? ReopenedBy, string? ReopenReason, Guid? ReversalJournalId, bool AlreadyClosed = false)
{
    /// <summary>The list projection (translated to SQL).</summary>
    public static readonly Expression<Func<FiscalYearClose, FiscalYearCloseDto>> Projection =
        c => new FiscalYearCloseDto(c.Id, c.LegalEntityId, c.FiscalYear, c.YearStart, c.YearEnd, c.JournalEntryId, c.NetResult, c.PreviousBooksLockedTo, c.ClosedAt, c.ClosedBy, c.ReopenedAt, c.ReopenedBy, c.ReopenReason, c.ReversalJournalId, false);

    public static FiscalYearCloseDto From(FiscalYearClose c, bool alreadyClosed = false) => new(
        c.Id, c.LegalEntityId, c.FiscalYear, c.YearStart, c.YearEnd, c.JournalEntryId, c.NetResult, c.PreviousBooksLockedTo,
        c.ClosedAt, c.ClosedBy, c.ReopenedAt, c.ReopenedBy, c.ReopenReason, c.ReversalJournalId, alreadyClosed);
}
