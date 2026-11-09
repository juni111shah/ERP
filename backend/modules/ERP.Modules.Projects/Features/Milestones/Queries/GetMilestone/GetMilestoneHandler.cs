using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetMilestoneHandler(IProjectMilestoneRepository milestones, ProjectAccess access, MilestoneDtoBuilder dtos) : IQueryHandler<GetMilestoneQuery, MilestoneDto>
{
    public async Task<MilestoneDto> Handle(GetMilestoneQuery query, CancellationToken ct)
    {
        var m = await milestones.GetAsync(query.Id, ct);
        await access.EnsureProjectAsync(m.ProjectId, ct);
        return await dtos.BuildAsync(m, ct);
    }
}
