using ERP.Application.Abstractions;
using ERP.Domain.Platform;

namespace ERP.Application.Audit;

/// <summary>Turns audit events into log rows, with user names and the values hidden from callers who may not read them.</summary>
internal sealed class AuditRows(IUserDirectory directory, AuditFieldAccess access)
{
    public async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<AuditEvent> events, CancellationToken ct)
    {
        var ids = events.Where(a => a.UserId != null).Select(a => a.UserId!.Value).Distinct().ToList();
        return ids.Count == 0 ? [] : (await directory.ListMembersAsync(ids, ct)).ToDictionary(u => u.Id, u => u.FullName);
    }

    public AuditRow ToRow(AuditEvent a, Dictionary<Guid, string> names)
    {
        var canRead = access.CanReadValues(a.RecordType);
        var snapshot = canRead ? AuditSnapshot.Latest(a) : new Dictionary<string, string?>();
        return new AuditRow(a.Id, a.OccurredAt, a.UserId, a.UserId is { } u ? names.GetValueOrDefault(u) : null, a.Action, a.RecordType, a.RecordId,
            AuditSnapshot.Label(snapshot), snapshot.GetValueOrDefault("Direction"), AuditSnapshot.ChangedFields(a), a.IpAddress, !canRead);
    }
}
