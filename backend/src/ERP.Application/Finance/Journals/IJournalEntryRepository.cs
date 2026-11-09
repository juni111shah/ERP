using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for journal entries. Implemented in ERP.Infrastructure/Repositories/Finance/JournalEntryRepository.cs.</summary>
public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    /// <summary>The journal with its lines, tracked for changes (to reverse it). Throws NotFoundException.</summary>
    Task<JournalEntry> GetWithLinesForUpdateAsync(Guid? id, CancellationToken ct);

    /// <summary>Every non-draft ledger line on <paramref name="accountId"/> dated on or before <paramref name="asOf"/> (all legal entities when <paramref name="entityId"/> is null).</summary>
    Task<List<AccountLedgerLine>> ListAccountLinesAsync(Guid accountId, Guid? entityId, DateOnly asOf, CancellationToken ct);

    /// <summary>Debit and credit of every non-draft ledger line on the accounts dated on or before <paramref name="asOf"/>.</summary>
    Task<List<AccountMovement>> ListMovementsAsync(List<Guid> accountIds, DateOnly asOf, Guid? entityId, CancellationToken ct);
}

/// <summary>One ledger line on an account, with its journal.</summary>
public sealed record AccountLedgerLine(Guid JournalLineId, Guid JournalId, string Number, DateOnly Date, string? Description, decimal Debit, decimal Credit);

/// <summary>The debit and credit of one ledger line on an account.</summary>
public sealed record AccountMovement(Guid AccountId, decimal Debit, decimal Credit);
