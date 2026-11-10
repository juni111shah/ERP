using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class ListAllocationsHandler(IProjectAllocationRepository allocations, ProjectAccess access) : IQueryHandler<ListAllocationsQuery, IReadOnlyList<AllocationDto>>
{
    public async Task<IReadOnlyList<AllocationDto>> Handle(ListAllocationsQuery query, CancellationToken ct)
    {
        var visible = await access.VisibleIdsAsync(ct);
        return (await allocations.ListByProjectAndUserAsync(query.ProjectId, query.UserId, ct))
            .Where(a => visible == null || visible.Contains(a.ProjectId))
            .OrderBy(a => a.FromDate).Select(a => new AllocationDto(a.Id, a.ProjectId, a.UserId, a.UserName, a.FromDate, a.ToDate, a.HoursPerWeek)).ToList();
    }
}
