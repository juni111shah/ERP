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
public sealed record IdsBody(IReadOnlyList<Guid> Ids);
public sealed record DecideBody(IReadOnlyList<Guid> Ids, bool Approve, string? Reason);
public sealed record DependenciesBody(IReadOnlyList<Guid> DependsOn);
public sealed record DecideChangeBody(bool Approve, string? Note);
public sealed record MemberBody(Guid UserId, string? Role);
public sealed record ArchiveBody(bool Archive);
public sealed record PostCostsBody(Guid ProjectId);
public sealed record BaselineBody(bool Clear);

internal static class ProjectsEndpoints
{
    private const string Base = "/api/v1/m/projects";

    /// <summary>A paged list that honours the members-only setting. <c>?live=true</c> leaves out templates and archived projects.</summary>
    private static void Scoped<T, TDto>(RouteGroupBuilder g, string permission, Func<IReadOnlyCollection<Guid>, System.Linq.Expressions.Expression<Func<T, bool>>> scope, System.Linq.Expressions.Expression<Func<T, TDto>> projection, Func<IServiceProvider, IReadOnlyList<TDto>, CancellationToken, Task<IReadOnlyList<TDto>>> enrich) where T : ERP.Domain.Common.Entity =>
        g.MapGet("", ([Microsoft.AspNetCore.Http.AsParameters] PagedQuery q, bool? live, ISender s, CancellationToken ct) => s.Send(new ScopedProjectList<T, TDto>(q, projection, permission, scope, enrich, live == true), ct)).RequirePermission(permission);

    public static void Map(RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects");
        Scoped<Project, ProjectSummaryDto>(projects, P.ProjectsRead, ids => p => ids.Contains(p.Id),
            p => new ProjectSummaryDto(p.Id, p.Number, p.Name, p.LegalEntityId, p.CustomerId, p.CustomerName, p.ManagerName, p.Status, p.BillingType, p.BudgetAmount, p.StartDate, p.EndDate, p.CreatedAt, p.ManagerUserId, p.IsTemplate, p.IsArchived, 0m, null, 0m, 0m, 0, 0, 0m), ProjectListEnrich.Projects);
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
        projects.MapPost("/{id:guid}/clone", async (Guid id, CloneInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CloneProject(id, input), ct);
            return Results.Created($"{Base}/projects/{created.Id}", created);
        }).RequirePermission(P.ProjectsManage);
        projects.MapPost("/{id:guid}/archive", (Guid id, ArchiveBody b, ISender s, CancellationToken ct) => s.Send(new ArchiveProject(id, b.Archive), ct)).RequirePermission(P.ProjectsManage);
        projects.MapGet("/{id:guid}/schedule", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetProjectSchedule(id), ct)).RequirePermission(P.TasksRead);
        projects.MapPost("/{id:guid}/baseline", (Guid id, BaselineBody b, ISender s, CancellationToken ct) => s.Send(new SetProjectBaseline(id, b.Clear), ct)).RequirePermission(P.TasksManage);
        projects.MapGet("/{id:guid}/activity", (Guid id, int? take, ISender s, CancellationToken ct) => s.Send(new GetProjectActivity(id, take ?? 100), ct)).RequirePermission(P.ProjectsRead);
        projects.MapGet("/{id:guid}/budget-lines", (Guid id, ISender s, CancellationToken ct) => s.Send(new ListBudgetLines(id), ct)).RequirePermission(P.ProjectsRead);
        group.MapGet("/config", (ISender s, CancellationToken ct) => s.Send(new GetProjectsConfig(), ct)).RequireAuthorization();
        var budgetLines = group.MapGroup("/budget-lines");
        budgetLines.MapPost("", async (BudgetLineInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/budget-lines", await s.Send(new SaveBudgetLine(null, input), ct))).RequirePermission(P.ProjectsManage);
        budgetLines.MapPut("/{id:guid}", (Guid id, BudgetLineInput input, ISender s, CancellationToken ct) => s.Send(new SaveBudgetLine(id, input), ct)).RequirePermission(P.ProjectsManage);
        budgetLines.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteBudgetLine(id), ct); return Results.NoContent(); }).RequirePermission(P.ProjectsManage);
        projects.MapGet("/{id:guid}/members", (Guid id, ISender s, CancellationToken ct) => s.Send(new ListMembers(id), ct)).RequirePermission(P.ProjectsRead);
        projects.MapPost("/{id:guid}/members", async (Guid id, MemberBody b, ISender s, CancellationToken ct) => Results.Created($"{Base}/projects/{id}/members", await s.Send(new AddMember(id, b.UserId, b.Role), ct))).RequirePermission(P.TeamManage);
        projects.MapDelete("/members/{memberId:guid}", async (Guid memberId, ISender s, CancellationToken ct) => { await s.Send(new RemoveMember(memberId), ct); return Results.NoContent(); }).RequirePermission(P.TeamManage);

        var tasks = group.MapGroup("/tasks");
        Scoped<ProjectTask, TaskDto>(tasks, P.TasksRead, ids => t => ids.Contains(t.ProjectId),
            t => new TaskDto(t.Id, t.ProjectId, "", t.MilestoneId, null, t.Number, t.Title, t.Description, t.Status, t.Priority, t.AssignedTo, t.EstimatedHours, t.ActualHours, t.DueDate, t.CreatedAt, t.AssignedUserId, t.ParentTaskId, t.StartDate, t.Labels, null), ProjectListEnrich.Tasks);
        tasks.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetTask(id), ct)).RequirePermission(P.TasksRead);
        tasks.MapPost("", async (TaskInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateTask(input), ct);
            return Results.Created($"{Base}/tasks/{created.Id}", created);
        }).RequirePermission(P.TasksManage);
        tasks.MapPut("/{id:guid}", (Guid id, TaskInput input, ISender s, CancellationToken ct) => s.Send(new UpdateTask(id, input), ct)).RequirePermission(P.TasksManage);
        tasks.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteTask(id), ct); return Results.NoContent(); }).RequirePermission(P.TasksManage);
        tasks.MapPut("/{id:guid}/status", (Guid id, ChangeTaskStatusBody b, ISender s, CancellationToken ct) => s.Send(new ChangeTaskStatus(id, b.Status), ct)).RequirePermission(P.TasksManage);
        tasks.MapPut("/{id:guid}/dependencies", (Guid id, DependenciesBody b, ISender s, CancellationToken ct) => s.Send(new SetTaskDependencies(id, b.DependsOn), ct)).RequirePermission(P.TasksManage);

        var milestones = group.MapGroup("/milestones");
        Scoped<ProjectMilestone, MilestoneDto>(milestones, P.MilestonesRead, ids => m => ids.Contains(m.ProjectId),
            m => new MilestoneDto(m.Id, m.ProjectId, "", m.Title, m.DueDate, m.Amount, m.Status, m.AchievedDate, m.CreatedAt), ProjectListEnrich.Milestones);
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
        Scoped<ProjectTimesheet, TimesheetDto>(timesheets, P.TimesheetsRead, ids => ts => ids.Contains(ts.ProjectId),
            ts => new TimesheetDto(ts.Id, ts.ProjectId, "", ts.TaskId, null, ts.Date, ts.WorkerName, ts.Hours, ts.HourlyRate, ts.TotalAmount, ts.Description, ts.IsBillable, ts.IsBilled, ts.InvoiceId, ts.CreatedAt,
                ts.WorkerUserId, ts.CostRate, ts.CostAmount, ts.ApprovalStatus, ts.RejectReason, ts.WrittenOff), ProjectListEnrich.Timesheets);
        timesheets.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetTimesheet(id), ct)).RequirePermission(P.TimesheetsRead);
        timesheets.MapPost("", async (TimesheetInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateTimesheet(input), ct);
            return Results.Created($"{Base}/timesheets/{created.Id}", created);
        }).RequirePermission(P.TimesheetsManage);
        timesheets.MapPut("/{id:guid}", (Guid id, TimesheetInput input, ISender s, CancellationToken ct) => s.Send(new UpdateTimesheet(id, input), ct)).RequirePermission(P.TimesheetsManage);
        timesheets.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteTimesheet(id), ct); return Results.NoContent(); }).RequirePermission(P.TimesheetsManage);
        timesheets.MapPost("/week", (SaveTimesheetWeekBody b, ISender s, CancellationToken ct) => s.Send(new SaveTimesheetWeek(b.Items), ct)).RequirePermission(P.TimesheetsManage);
        timesheets.MapPost("/submit", async (IdsBody b, ISender s, CancellationToken ct) => new { submitted = await s.Send(new SubmitTimesheets(b.Ids), ct) }).RequirePermission(P.TimesheetsManage);
        timesheets.MapPost("/decide", async (DecideBody b, ISender s, CancellationToken ct) => new { decided = await s.Send(new DecideTimesheets(b.Ids, b.Approve, b.Reason), ct) }).RequirePermission(P.TimesheetsApprove);
        timesheets.MapPost("/write-off", async (IdsBody b, ISender s, CancellationToken ct) => new { writtenOff = await s.Send(new WriteOffTimesheets(b.Ids), ct) }).RequirePermission(P.BillingManage);

        var expenses = group.MapGroup("/expenses");
        Scoped<ProjectExpense, ExpenseDto>(expenses, P.ExpensesRead, ids => e => ids.Contains(e.ProjectId),
            e => new ExpenseDto(e.Id, e.ProjectId, "", e.Date, e.Category, e.Description, e.Amount, e.IsBillable, e.MarkupPercent, e.Amount * (1 + e.MarkupPercent / 100m), e.WorkerUserId, e.WorkerName, e.Status, e.RejectReason, e.IsBilled, e.InvoiceId, e.WrittenOff, e.CreatedAt), ProjectListEnrich.Expenses);
        expenses.MapGet("/{id:guid}", (Guid id, ISender s, CancellationToken ct) => s.Send(new GetExpense(id), ct)).RequirePermission(P.ExpensesRead);
        expenses.MapPost("", async (ExpenseInput input, ISender s, CancellationToken ct) =>
        {
            var created = await s.Send(new CreateExpense(input), ct);
            return Results.Created($"{Base}/expenses/{created.Id}", created);
        }).RequirePermission(P.ExpensesManage);
        expenses.MapPut("/{id:guid}", (Guid id, ExpenseInput input, ISender s, CancellationToken ct) => s.Send(new UpdateExpense(id, input), ct)).RequirePermission(P.ExpensesManage);
        expenses.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteExpense(id), ct); return Results.NoContent(); }).RequirePermission(P.ExpensesManage);
        expenses.MapPost("/submit", async (IdsBody b, ISender s, CancellationToken ct) => new { submitted = await s.Send(new SubmitExpenses(b.Ids), ct) }).RequirePermission(P.ExpensesManage);
        expenses.MapPost("/decide", async (DecideBody b, ISender s, CancellationToken ct) => new { decided = await s.Send(new DecideExpenses(b.Ids, b.Approve, b.Reason), ct) }).RequirePermission(P.ExpensesApprove);
        expenses.MapPost("/write-off", async (IdsBody b, ISender s, CancellationToken ct) => new { writtenOff = await s.Send(new WriteOffExpenses(b.Ids), ct) }).RequirePermission(P.BillingManage);

        var billing = group.MapGroup("/billing");
        billing.MapGet("/overview", (ISender s, CancellationToken ct) => s.Send(new GetBillingOverview(), ct)).RequirePermission(P.BillingRead);
        billing.MapGet("/{projectId:guid}/billable", (Guid projectId, ISender s, CancellationToken ct) => s.Send(new GetBillableWork(projectId), ct)).RequirePermission(P.BillingRead);
        billing.MapGet("/{projectId:guid}/invoices", (Guid projectId, ISender s, CancellationToken ct) => s.Send(new GetProjectInvoices(projectId), ct)).RequirePermission(P.BillingRead);
        billing.MapPost("/write-off", async (PostCostsBody b, ISender s, CancellationToken ct) => new { writtenOff = await s.Send(new WriteOffProject(b.ProjectId), ct) }).RequirePermission(P.BillingManage);
        billing.MapPost("/bill", (BillProjectInput input, ISender s, CancellationToken ct) => s.Send(new BillProject(input), ct)).RequirePermission(P.BillingManage);
        billing.MapPost("/post-costs", (PostCostsBody b, ISender s, CancellationToken ct) => s.Send(new PostProjectCosts(b.ProjectId), ct)).RequirePermission(P.BillingManage);
        billing.MapPost("/preview", (BillProjectInput input, ISender s, CancellationToken ct) => s.Send(new PreviewProjectBill(input), ct)).RequirePermission(P.BillingRead);

        var rates = group.MapGroup("/rates");
        rates.MapGet("", (Guid? projectId, ISender s, CancellationToken ct) => s.Send(new ListRates(projectId), ct)).RequirePermission(P.RatesRead);
        rates.MapPost("", async (RateInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/rates", await s.Send(new SaveRate(null, input), ct))).RequirePermission(P.RatesManage);
        rates.MapPut("/{id:guid}", (Guid id, RateInput input, ISender s, CancellationToken ct) => s.Send(new SaveRate(id, input), ct)).RequirePermission(P.RatesManage);
        rates.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteRate(id), ct); return Results.NoContent(); }).RequirePermission(P.RatesManage);

        var comments = group.MapGroup("/comments");
        comments.MapGet("", (string ownerType, Guid ownerId, ISender s, CancellationToken ct) => s.Send(new ListComments(ownerType, ownerId), ct)).RequirePermission(P.ProjectsRead);
        comments.MapPost("", async (CommentInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/comments", await s.Send(new AddComment(input), ct))).RequirePermission(P.ProjectsRead);
        comments.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteComment(id), ct); return Results.NoContent(); }).RequirePermission(P.ProjectsRead);

        var changes = group.MapGroup("/change-orders");
        changes.MapGet("", (Guid? projectId, ISender s, CancellationToken ct) => s.Send(new ListChangeOrders(projectId), ct)).RequirePermission(P.ChangeOrdersRead);
        changes.MapPost("", async (ChangeOrderInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/change-orders", await s.Send(new CreateChangeOrder(input), ct))).RequirePermission(P.ChangeOrdersManage);
        changes.MapPut("/{id:guid}", (Guid id, ChangeOrderInput input, ISender s, CancellationToken ct) => s.Send(new UpdateChangeOrder(id, input), ct)).RequirePermission(P.ChangeOrdersManage);
        changes.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteChangeOrder(id), ct); return Results.NoContent(); }).RequirePermission(P.ChangeOrdersManage);
        changes.MapPost("/{id:guid}/decide", (Guid id, DecideChangeBody b, ISender s, CancellationToken ct) => s.Send(new DecideChangeOrder(id, b.Approve, b.Note), ct)).RequirePermission(P.ChangeOrdersManage);

        var risks = group.MapGroup("/risks");
        risks.MapGet("", (Guid? projectId, ISender s, CancellationToken ct) => s.Send(new ListRisks(projectId), ct)).RequirePermission(P.RisksRead);
        risks.MapPost("", async (RiskInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/risks", await s.Send(new SaveRisk(null, input), ct))).RequirePermission(P.RisksManage);
        risks.MapPut("/{id:guid}", (Guid id, RiskInput input, ISender s, CancellationToken ct) => s.Send(new SaveRisk(id, input), ct)).RequirePermission(P.RisksManage);
        risks.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteRisk(id), ct); return Results.NoContent(); }).RequirePermission(P.RisksManage);

        var allocations = group.MapGroup("/allocations");
        allocations.MapGet("", (Guid? projectId, Guid? userId, ISender s, CancellationToken ct) => s.Send(new ListAllocations(projectId, userId), ct)).RequirePermission(P.ProjectsRead);
        allocations.MapPost("", async (AllocationInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/allocations", await s.Send(new SaveAllocation(null, input), ct))).RequirePermission(P.TeamManage);
        allocations.MapPut("/{id:guid}", (Guid id, AllocationInput input, ISender s, CancellationToken ct) => s.Send(new SaveAllocation(id, input), ct)).RequirePermission(P.TeamManage);
        allocations.MapDelete("/{id:guid}", async (Guid id, ISender s, CancellationToken ct) => { await s.Send(new DeleteAllocation(id), ct); return Results.NoContent(); }).RequirePermission(P.TeamManage);

        var accept = group.MapGroup("/acceptances");
        accept.MapGet("", (Guid? projectId, ISender s, CancellationToken ct) => s.Send(new ListAcceptances(projectId), ct)).RequirePermission(P.MilestonesRead);
        accept.MapPost("", async (RequestAcceptanceInput input, ISender s, CancellationToken ct) => Results.Created($"{Base}/acceptances", await s.Send(new RequestAcceptance(input), ct))).RequirePermission(P.MilestonesManage);

        var portal = group.MapGroup("/portal");
        portal.MapGet("/projects", (ISender s, CancellationToken ct) => s.Send(new GetPortalProjects(), ct)).RequireAuthorization("portal.customer");
        portal.MapGet("/acceptances", (ISender s, CancellationToken ct) => s.Send(new ListPortalAcceptances(), ct)).RequireAuthorization("portal.customer");
        portal.MapPost("/acceptances/{id:guid}/sign", (Guid id, SignAcceptanceInput body, ISender s, CancellationToken ct) => s.Send(new SignAcceptance(id, body), ct)).RequireAuthorization("portal.customer");

        var reports = group.MapGroup("/reports");
        reports.MapGet("/profitability", (DateOnly? from, DateOnly? to, Guid? projectId, ISender s, CancellationToken ct) => s.Send(new GetProfitabilityReport(from, to, projectId), ct)).RequirePermission(P.ReportsRead);
        reports.MapGet("/utilisation", (DateOnly from, DateOnly to, ISender s, CancellationToken ct) => s.Send(new GetUtilisationReport(from, to), ct)).RequirePermission(P.ReportsRead);
        reports.MapGet("/budget", (ISender s, CancellationToken ct) => s.Send(new GetBudgetReport(), ct)).RequirePermission(P.ReportsRead);
        reports.MapGet("/unbilled-ageing", (DateOnly? asOf, ISender s, CancellationToken ct) => s.Send(new GetUnbilledAgeing(asOf), ct)).RequirePermission(P.ReportsRead);
        reports.MapGet("/hours", (DateOnly from, DateOnly to, string? groupBy, Guid? projectId, ISender s, CancellationToken ct) => s.Send(new GetHoursReport(from, to, groupBy ?? "person", projectId), ct)).RequirePermission(P.ReportsRead);
    }
}

public sealed record SaveTimesheetWeekBody(IReadOnlyList<TimesheetInput> Items);
