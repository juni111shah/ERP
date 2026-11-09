using ERP.Application.Abstractions;
using ERP.Domain.Finance;

namespace ERP.Modules.Pos.Repositories;

/// <summary>The journals the till posts. Implemented in PosJournalRepository.cs.</summary>
public interface IPosJournalRepository : IRepository<JournalEntry>
{
    /// <summary>The posted journal of a source document, with its lines, tracked for changes (to reverse it); null when there is none.</summary>
    Task<JournalEntry?> FindPostedForSourceForUpdateAsync(string sourceType, Guid sourceId, CancellationToken ct);
}
