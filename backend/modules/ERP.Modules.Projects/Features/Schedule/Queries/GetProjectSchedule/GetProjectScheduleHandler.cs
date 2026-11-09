using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectScheduleHandler(ProjectScheduleBuilder schedules) : IQueryHandler<GetProjectScheduleQuery, ScheduleDto>
{
    public Task<ScheduleDto> Handle(GetProjectScheduleQuery query, CancellationToken ct) => schedules.BuildAsync(query.ProjectId, ct);
}
