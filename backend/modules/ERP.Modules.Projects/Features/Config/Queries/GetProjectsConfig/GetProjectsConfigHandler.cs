using ERP.Application.Abstractions;
using ERP.Application.Common;

namespace ERP.Modules.Projects.Features;

internal sealed class GetProjectsConfigHandler(ISettingsService settings) : IQueryHandler<GetProjectsConfigQuery, ProjectsConfigDto>
{
    public async Task<ProjectsConfigDto> Handle(GetProjectsConfigQuery query, CancellationToken ct)
    {
        var week = ReportMath.ParseWorkWeek(await settings.GetAsync<string?>("projects.workWeekDays", null, ct));
        return new ProjectsConfigDto(
            await settings.GetAsync<bool?>("projects.timesheetApproval", null, ct) == true,
            await settings.GetAsync<bool?>("projects.expenseApproval", null, ct) == true,
            await settings.GetAsync<bool?>("projects.allowSelfApproval", null, ct) == true,
            await settings.GetAsync<decimal?>("projects.maxHoursPerDay", null, ct) ?? 16m,
            await settings.GetAsync<decimal?>("projects.weeklyCapacityHours", null, ct) ?? 40m,
            week.Select(d => (int)d).OrderBy(d => d).ToList(),
            await settings.GetAsync<decimal?>("projects.defaultHourlyRate", null, ct) ?? 0m);
    }
}
