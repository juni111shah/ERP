using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListRisksHandler(IProjectRiskRepository risks, ProjectAccess access) : IQueryHandler<ListRisksQuery, IReadOnlyList<RiskDto>>
{
    public async Task<IReadOnlyList<RiskDto>> Handle(ListRisksQuery query, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        var rows = (await risks.ListByProjectAsync(query.ProjectId, ct)).Where(r => visible == null || visible.Contains(r.ProjectId)).ToList();
        return rows.OrderBy(r => r.Status).ThenByDescending(r => r.Severity).ThenBy(r => r.DueDate).Select(RiskDto.From).ToList();
    }
}
