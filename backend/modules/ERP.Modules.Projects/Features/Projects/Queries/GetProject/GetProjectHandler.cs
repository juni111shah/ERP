using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectHandler(IProjectRepository projects, ProjectAccess access, ProjectDtoBuilder dtos) : IQueryHandler<GetProjectQuery, ProjectDto>
{
    public async Task<ProjectDto> Handle(GetProjectQuery query, CancellationToken ct)
    {
        var p = await projects.GetAsync(query.Id, ct);
        await access.EnsureAsync(p, ct);
        return await dtos.BuildAsync(p, ct);
    }
}
