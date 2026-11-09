using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Application.Finance;

/// <summary>Data access for journal entries. Implemented in ERP.Infrastructure/Repositories/Finance/JournalEntryRepository.cs.</summary>
public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    /// <summary>The journal entry with its lines, read-only. Throws NotFoundException.</summary>
    Task<JournalEntry> GetWithLinesAsync(Guid id, CancellationToken ct);

    /// <summary>Every line of the non-draft entries dated on or before <paramref name="to"/>, of one legal entity or (null) all of them.</summary>
    Task<List<PostedJournalLine>> ListPostedLinesUpToAsync(DateOnly to, Guid? legalEntityId, CancellationToken ct);
}

/// <summary>One posted journal line with its entry's date.</summary>
public sealed record PostedJournalLine(Guid AccountId, DateOnly Date, decimal Debit, decimal Credit, Guid? BusinessUnitId);
