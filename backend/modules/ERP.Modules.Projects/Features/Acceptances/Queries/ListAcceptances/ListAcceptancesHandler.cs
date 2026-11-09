using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListAcceptancesHandler(IProjectAcceptanceRepository acceptances, IProjectRepository projects, ProjectAccess access) : IQueryHandler<ListAcceptancesQuery, IReadOnlyList<AcceptanceDto>>
{
    public async Task<IReadOnlyList<AcceptanceDto>> Handle(ListAcceptancesQuery query, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var rows = (await acceptances.ListByProjectAsync(query.ProjectId, ct)).Where(a => visible == null || visible.Contains(a.ProjectId)).ToList();
        var ids = rows.Select(a => a.ProjectId).Distinct().ToList();
        var names = await projects.MapNamesAsync(ids, ct);
        return rows.OrderByDescending(a => a.RequestedAt).Select(a => new AcceptanceDto(a.Id, a.ProjectId, names.GetValueOrDefault(a.ProjectId, ""), a.MilestoneId, a.Title, a.Status, a.RequestedAt, a.SignedByName, a.SignedAt, a.Comment, a.SignatureImage != null, a.SignedIp, a.EvidenceHash)).ToList();
    }
}
