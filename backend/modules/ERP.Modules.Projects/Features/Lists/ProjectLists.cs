using ERP.Application.Common;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>The scoped list queries of the module, one per list screen: entity, projection, permission, members-only scope and enrichment.</summary>
public static class ProjectLists
{
    public static ScopedProjectListQuery<Project, ProjectSummaryDto> Projects(PagedQuery paging, bool liveOnly)
        => new(paging, ProjectSummaryDto.Projection, P.ProjectsRead, ids => p => ids.Contains(p.Id), ProjectListEnrich.Projects, liveOnly);

    public static ScopedProjectListQuery<ProjectTask, TaskDto> Tasks(PagedQuery paging, bool liveOnly)
        => new(paging, TaskDto.Projection, P.TasksRead, ids => t => ids.Contains(t.ProjectId), ProjectListEnrich.Tasks, liveOnly);

    public static ScopedProjectListQuery<ProjectMilestone, MilestoneDto> Milestones(PagedQuery paging, bool liveOnly)
        => new(paging, MilestoneDto.Projection, P.MilestonesRead, ids => m => ids.Contains(m.ProjectId), ProjectListEnrich.Milestones, liveOnly);

    public static ScopedProjectListQuery<ProjectTimesheet, TimesheetDto> Timesheets(PagedQuery paging, bool liveOnly)
        => new(paging, TimesheetDto.Projection, P.TimesheetsRead, ids => ts => ids.Contains(ts.ProjectId), ProjectListEnrich.Timesheets, liveOnly);

    public static ScopedProjectListQuery<ProjectExpense, ExpenseDto> Expenses(PagedQuery paging, bool liveOnly)
        => new(paging, ExpenseDto.Projection, P.ExpensesRead, ids => e => ids.Contains(e.ProjectId), ProjectListEnrich.Expenses, liveOnly);
}
