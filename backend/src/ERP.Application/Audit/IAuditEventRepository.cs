using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Audit;

/// <summary>Data access for the audit trail. Implemented in ERP.Infrastructure/Repositories/Audit/AuditEventRepository.cs.</summary>
public interface IAuditEventRepository : IRepository<AuditEvent>
{
    /// <summary>Up to <paramref name="take"/> other events written by the same save as <paramref name="e"/> (same time and user), by record type.</summary>
    Task<List<AuditEvent>> ListSameSaveAsync(AuditEvent e, int take, CancellationToken ct);

    /// <summary>Every record type in the log, sorted.</summary>
    Task<List<string>> ListRecordTypesAsync(CancellationToken ct);

    /// <summary>Every action in the log, sorted.</summary>
    Task<List<string>> ListActionsAsync(CancellationToken ct);

    /// <summary>Every user who appears in the log.</summary>
    Task<List<Guid>> ListUserIdsAsync(CancellationToken ct);

    /// <summary>How many users wrote to the log since <paramref name="since"/>.</summary>
    Task<int> CountActiveUsersSinceAsync(DateTime since, CancellationToken ct);

    /// <summary>One page of a record's events, newest first.</summary>
    Task<List<AuditEvent>> ListPageForRecordAsync(string recordType, string recordId, int page, int pageSize, CancellationToken ct);

    /// <summary>The latest <paramref name="take"/> events of any of the records, newest first.</summary>
    Task<List<AuditEvent>> ListLatestForRecordsAsync(List<string> recordIds, int take, CancellationToken ct);
}
