using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetTaskHandler(IProjectTaskRepository tasks, ProjectAccess access, TaskDtoBuilder dtos) : IQueryHandler<GetTaskQuery, TaskDto>
{
    public async Task<TaskDto> Handle(GetTaskQuery query, CancellationToken ct)
    {
        var t = await tasks.GetAsync(query.Id, ct);
        await access.EnsureProjectAsync(t.ProjectId, ct);
        return await dtos.BuildAsync(t, ct);
    }
}
