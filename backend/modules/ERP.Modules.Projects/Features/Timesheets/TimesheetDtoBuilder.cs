using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Repositories;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the time entry view: project name, task title, and the cost only for people who may see it. Shared by the timesheet handlers.</summary>
public sealed class TimesheetDtoBuilder(IProjectRepository projects, IProjectTaskRepository tasks, ProjectAccess access)
{
    public async Task<TimesheetDto> BuildAsync(ProjectTimesheet ts, CancellationToken ct)
    {
        var p = await projects.GetAsync(ts.ProjectId, ct);
        string? tTitle = null;
        if (ts.TaskId is { } tid)
            tTitle = await tasks.GetTitleAsync(tid, ct);
        var cost = access.CanSeeCost;
        return new TimesheetDto(ts.Id, ts.ProjectId, p.Name, ts.TaskId, tTitle, ts.Date, ts.WorkerName, ts.Hours, ts.HourlyRate, ts.TotalAmount, ts.Description, ts.IsBillable, ts.IsBilled, ts.InvoiceId, ts.CreatedAt,
            ts.WorkerUserId, cost ? ts.CostRate : 0m, cost ? ts.CostAmount : 0m, ts.ApprovalStatus, ts.RejectReason, ts.WrittenOff);
    }
}
