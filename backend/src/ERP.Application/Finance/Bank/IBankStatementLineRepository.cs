using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for imported bank statement lines. Implemented in ERP.Infrastructure/Repositories/Finance/BankStatementLineRepository.cs.</summary>
public interface IBankStatementLineRepository : IRepository<BankStatementLine>
{
    /// <summary>Date, amount, reference and text of every line already imported for the account (to skip duplicates).</summary>
    Task<List<StatementLineKey>> ListKeysAsync(Guid accountId, Guid entityId, CancellationToken ct);

    /// <summary>The account's unmatched lines, oldest first, tracked for changes.</summary>
    Task<List<BankStatementLine>> ListUnmatchedForUpdateAsync(Guid accountId, Guid entityId, CancellationToken ct);

    /// <summary>The account's unmatched lines dated on or before <paramref name="asOf"/>, oldest first, read-only.</summary>
    Task<List<BankStatementLine>> ListUnmatchedAsOfAsync(Guid accountId, DateOnly asOf, Guid? entityId, CancellationToken ct);

    /// <summary>The ledger lines the account's statement lines are matched to.</summary>
    Task<HashSet<Guid>> MatchedJournalLineIdsAsync(Guid accountId, CancellationToken ct);
}
