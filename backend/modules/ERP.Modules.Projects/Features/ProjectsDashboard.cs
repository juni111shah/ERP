using ERP.Application.Common;
using ERP.Application.Abstractions;
using ERP.Application.Dashboard;
using ERP.Modules.Projects.Domain;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public sealed class ProjectsDashboard(
    IReadRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<ProjectTimesheet> timesheets,
    ICurrentUser user) : IDashboardContributor
{
    public string ModuleKey => ProjectsModule.Key;

    public async Task<DashboardContribution> BuildAsync(DashboardScope scope, CancellationToken ct)
    {
        var entity = scope.LegalEntityId;
        var kpis = new List<KpiCard>();
        var charts = new List<DashboardChart>();

        if (user.HasPermission(P.ProjectsRead))
        {
            var active = await projects.CountAsync(p => p.Status == ProjectStatus.Active && (entity == null || p.LegalEntityId == entity), ct);
            kpis.Add(new("active-projects", "Active Projects", active, "number", "projects in progress", 130, "/projects/list"));
        }

        if (user.HasPermission(P.TasksRead))
        {
            var pendingTasks = await tasks.CountAsync(t => (t.Status == ProjectTaskStatus.Todo || t.Status == ProjectTaskStatus.InProgress), ct);
            kpis.Add(new("pending-tasks", "Open Tasks", pendingTasks, "number", "tasks to complete", 135, "/projects/tasks"));
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var overdue = await tasks.CountAsync(t => t.DueDate != null && t.DueDate < today && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled, ct);
            if (overdue > 0) kpis.Add(new("overdue-tasks", "Overdue Tasks", overdue, "number", "past their due date", 136, "/projects/tasks"));

            var taskStatuses = new[] { ProjectTaskStatus.Todo, ProjectTaskStatus.InProgress, ProjectTaskStatus.Review, ProjectTaskStatus.Done, ProjectTaskStatus.Blocked };
            var grouped = await tasks.Query().CountByAsync(t => t.Status, ct);
            var taskCounts = taskStatuses.Select(k => (decimal)grouped.GetValueOrDefault(k)).ToList();
            charts.Add(new("project-tasks-status", "Project tasks by status", "hbar",
                ["To Do", "In Progress", "In Review", "Done", "Blocked"],
                [new("Tasks", taskCounts)], "Task workflow progress", 60));
        }

        if (user.HasPermission(P.BillingRead))
        {
            var unbilledTs = await timesheets.Query(new Specification<ProjectTimesheet>(ts => ts.IsBillable && !ts.IsBilled && !ts.WrittenOff && ts.ApprovalStatus == TimesheetStatus.Approved)).ToListAsync(ct);
            var unbilledAmount = unbilledTs.Sum(x => x.TotalAmount);
            kpis.Add(new("unbilled-project-work", "Unbilled Hours", unbilledTs.Sum(x => x.Hours), "number", $"unbilled value {unbilledAmount:N0}", 140, "/projects/billing"));
        }

        if (user.HasPermission(P.TimesheetsApprove))
        {
            var waiting = await timesheets.CountAsync(ts => ts.ApprovalStatus == TimesheetStatus.Submitted, ct);
            if (waiting > 0) kpis.Add(new("timesheets-waiting", "Timesheets to approve", waiting, "number", "waiting for a decision", 141, "/projects/approvals"));
        }

        return new DashboardContribution(kpis, charts, []);
    }
}
