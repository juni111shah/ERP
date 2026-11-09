using ERP.Application.Common;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

internal sealed class GetTimesheetHandler(IProjectTimesheetRepository timesheets, ProjectAccess access, TimesheetDtoBuilder dtos) : IQueryHandler<GetTimesheetQuery, TimesheetDto>
{
    public async Task<TimesheetDto> Handle(GetTimesheetQuery query, CancellationToken ct)
    {
        var ts = await timesheets.GetAsync(query.Id, ct);
        await access.EnsureProjectAsync(ts.ProjectId, ct);
        return await dtos.BuildAsync(ts, ct);
    }
}
