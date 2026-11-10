using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Application.Audit;

internal sealed class GetAuditFacetsHandler(IAuditEventRepository audits, IUserDirectory directory, IClock clock) : IQueryHandler<GetAuditFacetsQuery, AuditFacets>
{
    public async Task<AuditFacets> Handle(GetAuditFacetsQuery query, CancellationToken ct)
    {
        // 1. What the log can be filtered by.
        var recordTypes = await audits.ListRecordTypesAsync(ct);
        var actions = await audits.ListActionsAsync(ct);
        var userIds = await audits.ListUserIdsAsync(ct);
        var users = (await directory.ListMembersAsync(userIds, ct)).Select(u => new AuditUserOption(u.Id, u.FullName)).OrderBy(u => u.Name).ToList();

        // 2. Recent activity.
        var today = clock.UtcNow.Date;
        var week = today.AddDays(-6);
        return new AuditFacets(recordTypes, actions, users,
            await audits.CountAsync(a => a.OccurredAt >= today, ct),
            await audits.CountAsync(a => a.OccurredAt >= week, ct),
            await audits.CountAsync(a => a.OccurredAt >= week && a.Action == AuditSnapshot.Deleted, ct),
            await audits.CountActiveUsersSinceAsync(week, ct));
    }
}
