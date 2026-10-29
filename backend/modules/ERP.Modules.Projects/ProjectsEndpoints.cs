using ERP.Application.Common;
using ERP.Application.Security;
using ERP.Modularity;
using ERP.Modules.Projects.Domain;
using ERP.Modules.Projects.Features;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ERP.Modules.Projects;

public sealed record ChangeProjectStatusBody(ProjectStatus Status);
public sealed record ChangeTaskStatusBody(ProjectTaskStatus Status);
public sealed record AchieveMilestoneBody(DateOnly? AchievedDate);

internal static class ProjectsEndpoints
{
    private const string Base = "/api/v1/m/projects";

    public static void Map(RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects");
        projects.MapPagedList<Project, ProjectSummaryDto>("", P.ProjectsRead,
            p => new ProjectSummaryDto(p.Id, p.Number, p.Name, p.LegalEntityId, p.CustomerId, p.CustomerName, p.ManagerName, p.Status, p.BillingType, p.BudgetAmount, p.StartDate, p.EndDate, p.CreatedAt));
        projects.MapGet("/kpis", (ISender s, CancellationToken ct) => s.Send(new GetProjectKpiSummary(), ct)).RequirePermission(P.ProjectsRead);
        projects.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetProject(id), ct)).RequirePermission(P.ProjectsRead);
        projects.MapPost("", async (ProjectInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateProject(input), ct);
            return Results.Created($"{Base}/projects/{created.Id}", created);
        }).RequirePermission(P.ProjectsManage);
        projects.MapPut("/{id:guid}", (Guid id, ProjectInput input, ISender s, CancellationToken ct) => s.Send(new UpdateProject(id, input), ct)).RequirePermission(P.ProjectsManage);
        projects.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteProject(id), ct); return Results.NoContent(); }).RequirePermission(P.ProjectsManage);
        projects.MapPut("/{id:guid}/status", (Guid id, ChangeProjectStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeProjectStatus(id, b.Status), ct)).RequirePermission(P.ProjectsManage);

        var tasks = group.MapGroup("/tasks");
        tasks.MapPagedList<ProjectTask, TaskDto>("", P.TasksRead,
            t => new TaskDto(t.Id, t.ProjectId, "", t.MilestoneId, null, t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssignedTo, t.EstimatedHours, t.ActualHours, t.DueDate, t.CreatedAt));
        tasks.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetTask(id), ct)).RequirePermission(P.TasksRead);
        tasks.MapPost("", async (TaskInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateTask(input), ct);
            return Results.Created($"{Base}/tasks/{created.Id}", created);
        }).RequirePermission(P.TasksManage);
        tasks.MapPut("/{id:guid}", (Guid id, TaskInput input, ISender s, CancellationToken ct) => s.Send(new UpdateTask(id, input), ct)).RequirePermission(P.TasksManage);
        tasks.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteTask(id), ct); return Results.NoContent(); }).RequirePermission(P.TasksManage);
        tasks.MapPut("/{id:guid}/status", (Guid id, ChangeTaskStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeTaskStatus(id, b.Status), ct)).RequirePermission(P.TasksManage);

        var milestones = group.MapGroup("/milestones");
        milestones.MapPagedList<ProjectMilestone, MilestoneDto>("", P.MilestonesRead,
            m => new MilestoneDto(m.Id, m.ProjectId, "", m.Title, m.DueDate, m.Amount, m.Status, m.AchievedDate, m.CreatedAt));
        milestones.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetMilestone(id), ct)).RequirePermission(P.MilestonesRead);
        milestones.MapPost("", async (MilestoneInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateMilestone(input), ct);
            return Results.Created($"{Base}/milestones/{created.Id}", created);
        }).RequirePermission(P.MilestonesManage);
        milestones.MapPut("/{id:guid}", (Guid id, MilestoneInput input, ISender s, CancellationToken ct) => s.Send(new UpdateMilestone(id, input), ct)).RequirePermission(P.MilestonesManage);
        milestones.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteMilestone(id), ct); return Results.NoContent(); }).RequirePermission(P.MilestonesManage);
        milestones.MapPost("/{id:guid}/achieve", (Guid id, AchieveMilestoneBody b, ISender s, CancellationToken ct) => s.Send(new AchieveMilestone(id, b.AchievedDate), ct)).RequirePermission(P.MilestonesManage);

        var timesheets = group.MapGroup("/timesheets");
        timesheets.MapPagedList<ProjectTimesheet, TimesheetDto>("", P.TimesheetsRead,
            ts => new TimesheetDto(ts.Id, ts.ProjectId, "", ts.TaskId, null, ts.Date, ts.WorkerName, ts.Hours, ts.HourlyRate, ts.TotalAmount, ts.Description, ts.IsBillable, ts.IsBilled, ts.InvoiceId, ts.CreatedAt));
        timesheets.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetTimesheet(id), ct)).RequirePermission(P.TimesheetsRead);
        timesheets.MapPost("", async (TimesheetInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateTimesheet(input), ct);
            return Results.Created($"{Base}/timesheets/{created.Id}", created);
        }).RequirePermission(P.TimesheetsManage);
        timesheets.MapPut("/{id:guid}", (Guid id, TimesheetInput input, ISender s, CancellationToken ct) => s.Send(new UpdateTimesheet(id, input), ct)).RequirePermission(P.TimesheetsManage);
        timesheets.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteTimesheet(id), ct); return Results.NoContent(); }).RequirePermission(P.TimesheetsManage);

        var billing = group.MapGroup("/billing");
        billing.MapGet("/overview", (ISender s, CancellationToken ct) => s.Send(new GetBillingOverview(), ct)).RequirePermission(P.BillingRead);
        billing.MapPost("/bill", (BillProjectInput input, ISender s, CancellationToken ct) => s.Send(new BillProject(input), ct)).RequirePermission(P.BillingManage);
    }
}
