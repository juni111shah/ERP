using ERP.Application.Abstractions;
using ERP.Application.Audit;
using ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories.Audit;

internal sealed class AuditEventRepository(IRepository<AuditEvent> audits) : RepositoryBase<AuditEvent>(audits), IAuditEventRepository
{
    public Task<List<AuditEvent>> ListSameSaveAsync(AuditEvent e, int take, CancellationToken ct)
        => Query(new Specification<AuditEvent>(x => x.OccurredAt == e.OccurredAt && x.UserId == e.UserId && x.Id != e.Id)).OrderBy(x => x.RecordType).Take(take).ToListAsync(ct);

    public Task<List<string>> ListRecordTypesAsync(CancellationToken ct) => Query().Select(a => a.RecordType).Distinct().OrderBy(t => t).ToListAsync(ct);

    public Task<List<string>> ListActionsAsync(CancellationToken ct) => Query().Select(a => a.Action).Distinct().OrderBy(t => t).ToListAsync(ct);

    public Task<List<Guid>> ListUserIdsAsync(CancellationToken ct) => Query().Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToListAsync(ct);

    public Task<int> CountActiveUsersSinceAsync(DateTime since, CancellationToken ct)
        => Query().Where(a => a.OccurredAt >= since && a.UserId != null).Select(a => a.UserId).Distinct().CountAsync(ct);

    public Task<List<AuditEvent>> ListPageForRecordAsync(string recordType, string recordId, int page, int pageSize, CancellationToken ct)
        => Query(new Specification<AuditEvent>(a => a.RecordType == recordType && a.RecordId == recordId)).OrderByDescending(a => a.OccurredAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
}
