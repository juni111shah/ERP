using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for journals and the posted ledger. Implemented in ERP.Infrastructure/Repositories/Finance/JournalEntryRepository.cs.</summary>
public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    /// <summary>The journal with its lines, read-only. Throws NotFoundException.</summary>
    Task<JournalEntry> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>The journal with its lines, tracked for changes. Throws NotFoundException.</summary>
    Task<JournalEntry> GetWithLinesForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The opening entry (not a reversal) with its lines, tracked for changes; null when there is none.</summary>
    Task<JournalEntry?> FindOpeningEntryForUpdateAsync(Guid journalId, CancellationToken ct);

    /// <summary>Whether any journal line was ever posted to the account.</summary>
    Task<bool> HasPostingsAsync(Guid accountId, CancellationToken ct);

    /// <summary>Every line of non-draft journals dated on or before <paramref name="to"/>, optionally without the year-end closing entries.</summary>
    Task<List<LedgerMovement>> ListPostedMovementsAsync(DateOnly to, Guid? entityId, bool excludeYearEnd, CancellationToken ct);

    /// <summary>The lines of non-draft journals on one account, dated on or before <paramref name="to"/>.</summary>
    Task<List<AccountLedgerLine>> ListPostedLinesOfAccountAsync(Guid accountId, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The lines of non-draft journals of one entity on the given accounts, dated before <paramref name="before"/>.</summary>
    Task<List<LineAmounts>> ListPostedAmountsBeforeAsync(Guid entityId, DateOnly before, List<Guid> accountIds, CancellationToken ct);

    /// <summary>The lines of non-draft journals of one entity on the given accounts, dated on or before <paramref name="to"/>.</summary>
    Task<List<AccountMovement>> ListPostedMovementsOfAccountsAsync(Guid entityId, DateOnly to, List<Guid> accountIds, CancellationToken ct);

    /// <summary>Every line of non-draft journals of one entity dated in the range, leaving out the year-end closing entries.</summary>
    Task<List<AccountMovement>> ListPostedMovementsExcludingYearEndAsync(Guid entityId, DateOnly from, DateOnly to, CancellationToken ct);

    /// <summary>Every line of non-draft journals, on the given accounts only when <paramref name="accountIds"/> is set.</summary>
    Task<List<AccountMovement>> ListPostedMovementsAsync(IReadOnlyCollection<Guid>? accountIds, Guid? entityId, CancellationToken ct);

    /// <summary>The lines on the cash accounts of non-draft journals dated on or before <paramref name="to"/>.</summary>
    Task<List<CashLine>> ListPostedCashLinesAsync(List<Guid> cashIds, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>Every line of the non-draft, non-opening journals dated in the range that touch a cash account.</summary>
    Task<List<JournalLineAmounts>> ListLinesOfCashJournalsAsync(List<Guid> cashIds, DateOnly from, DateOnly to, Guid? entityId, CancellationToken ct);

    /// <summary>The lines on the given accounts of non-draft journals dated on or before <paramref name="to"/>, with their account.</summary>
    Task<List<CashAccountLine>> ListPostedCashAccountLinesAsync(List<Guid> accountIds, DateOnly to, Guid? entityId, CancellationToken ct);
}
