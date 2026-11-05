using System.Text.Json.Nodes;
using ERP.Application.Abstractions;
using ERP.Application.Common;
using ERP.Application.Customization;
using ERP.Application.Organizations;
using ERP.Application.Security;
using ERP.Domain.Organizations;
using ERP.Modules.Projects.Domain;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ERP.Modules.Projects.Features;

public static class Docs
{
    public const string Project = "project";
}

public sealed record ProjectInput(
    Guid LegalEntityId,
    string? Number,
    string Name,
    string? Description,
    Guid? CustomerId,
    string? CustomerName,
    string? ManagerName,
    ProjectStatus Status,
    ProjectBillingType BillingType,
    decimal BudgetAmount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    JsonObject? CustomFields = null,
    Guid? ManagerUserId = null,
    Guid? TaxCodeId = null,
    decimal BudgetHours = 0m,
    string? SourceType = null,
    Guid? SourceId = null,
    bool IsTemplate = false);

public sealed record ProjectSummaryDto(
    Guid Id,
    string Number,
    string Name,
    Guid LegalEntityId,
    Guid? CustomerId,
    string? CustomerName,
    string? ManagerName,
    ProjectStatus Status,
    ProjectBillingType BillingType,
    decimal BudgetAmount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTime CreatedAt,
    Guid? ManagerUserId = null,
    bool IsTemplate = false,
    bool IsArchived = false);

public sealed record ProjectDto(
    Guid Id,
    string Number,
    string Name,
    string? Description,
    Guid LegalEntityId,
    Guid? CustomerId,
    string? CustomerName,
    string? ManagerName,
    ProjectStatus Status,
    ProjectBillingType BillingType,
    decimal BudgetAmount,
    DateOnly? StartDate,
    DateOnly? EndDate,
    decimal TotalLoggedHours,
    decimal TotalBilledHours,
    decimal TotalTimesheetCost,
    int TotalTasks,
    int CompletedTasks,
    int TotalMilestones,
    int AchievedMilestones,
    JsonObject CustomFields,
    DateTime CreatedAt,
    Guid? ManagerUserId,
    Guid? TaxCodeId,
    decimal BudgetHours,
    bool IsTemplate,
    bool IsArchived,
    string? SourceType,
    Guid? SourceId,
    decimal EffectiveBudget,
    decimal ApprovedChanges,
    decimal BillableValue,
    decimal ExpenseCost,
    decimal BilledAmount,
    decimal UnbilledAmount,
    decimal PercentComplete,
    string Health,
    int OverdueTasks);

public sealed record ProjectKpiDto(
    int TotalProjects,
    int ActiveProjects,
    int CompletedProjects,
    decimal TotalBudget,
    decimal TotalLoggedHours,
    decimal UnbilledAmount);

public sealed record GetProject(Guid Id) : IQuery<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }
public sealed record CreateProject(ProjectInput Input) : ICommand<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record UpdateProject(Guid Id, ProjectInput Input) : ICommand<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record DeleteProject(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record ChangeProjectStatus(Guid Id, ProjectStatus Status) : ICommand<ProjectDto>, IRequirePermission { public string RequiredPermission => P.ProjectsManage; }
public sealed record GetProjectKpiSummary() : IQuery<ProjectKpiDto>, IRequirePermission { public string RequiredPermission => P.ProjectsRead; }

public sealed class ProjectInputValidator : AbstractValidator<ProjectInput>
{
    public ProjectInputValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Number).MaximumLength(30);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.CustomerName).MaximumLength(200);
        RuleFor(x => x.ManagerName).MaximumLength(150);
        RuleFor(x => x.BudgetAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BudgetHours).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SourceType).MaximumLength(60);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.BillingType).IsInEnum();
        RuleFor(x => x).Must(x => x.EndDate is null || x.StartDate is null || x.EndDate >= x.StartDate)
            .WithMessage("End date cannot be earlier than start date.");
    }
}

public sealed class CreateProjectValidator : AbstractValidator<CreateProject>
{
    public CreateProjectValidator() => RuleFor(x => x.Input).SetValidator(new ProjectInputValidator());
}

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProject>
{
    public UpdateProjectValidator() => RuleFor(x => x.Input).SetValidator(new ProjectInputValidator());
}

internal sealed class ProjectHandlers(
    IRepository<Project> projects,
    IReadRepository<ProjectTask> tasks,
    IReadRepository<ProjectMilestone> milestones,
    IReadRepository<ProjectTimesheet> timesheets,
    IReadRepository<ERP.Domain.Parties.Party> parties,
    IReadRepository<ProjectExpense> expenses,
    IReadRepository<ProjectChangeOrder> changeOrders,
    IReadRepository<ProjectProgressBilling> progress,
    ProjectPeople people,
    ProjectAccess access,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields) :
    IRequestHandler<GetProject, ProjectDto>,
    IRequestHandler<CreateProject, ProjectDto>,
    IRequestHandler<UpdateProject, ProjectDto>,
    IRequestHandler<DeleteProject, Unit>,
    IRequestHandler<ChangeProjectStatus, ProjectDto>,
    IRequestHandler<GetProjectKpiSummary, ProjectKpiDto>
{
    public async Task<ProjectDto> Handle(GetProject q, CancellationToken ct)
    {
        var p = await projects.GetAsync(q.Id, ct);
        await access.EnsureAsync(p, ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<ProjectDto> Handle(CreateProject cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        if (i.Status is not (ProjectStatus.Planning or ProjectStatus.Active))
            throw new FieldValidationException("status", "A new project starts as Planning or Active.");
        var customerName = await EnsureCustomerAsync(i.CustomerId, ct);
        var p = new Project { LegalEntityId = entity.Id };
        Map(i, p, customerName, await people.NameAsync(i.ManagerUserId, ct));
        p.Number = string.IsNullOrWhiteSpace(i.Number)
            ? await numbers.NextAsync(Docs.Project, entity.Id, null, ct)
            : i.Number.Trim();

        if (await projects.AnyAsync(x => x.Number == p.Number, ct))
            throw new ConflictException($"Project number '{p.Number}' is already in use.");

        await fields.ApplyAsync(Docs.Project, p, i.CustomFields, isCreate: true, ct);
        projects.Add(p);
        await projects.SaveChangesAsync(ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<ProjectDto> Handle(UpdateProject cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != p.LegalEntityId)
            await guard.RequireAsync(i.LegalEntityId, ct);

        var customerName = await EnsureCustomerAsync(i.CustomerId, ct);
        if (i.Status != p.Status) { ProjectsTransitions.Project.Ensure(p.Status, i.Status, isSystem: false); if (i.Status == ProjectStatus.Completed) await EnsureCanCompleteAsync(p, ct); }
        if (i.IsTemplate && !p.IsTemplate && await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct)) throw new ConflictException("A project that has time logged cannot become a template.");
        var number = string.IsNullOrWhiteSpace(i.Number) ? p.Number : i.Number.Trim();
        if (number != p.Number && await projects.AnyAsync(x => x.Number == number && x.Id != p.Id, ct))
            throw new ConflictException($"Project number '{number}' is already in use.");

        if (i.LegalEntityId != p.LegalEntityId && (await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct) || await tasks.AnyAsync(x => x.ProjectId == p.Id, ct)))
            throw new ConflictException("A project that already has tasks or time cannot move to another legal entity.");
        p.LegalEntityId = i.LegalEntityId;
        p.Number = number;
        Map(i, p, customerName, await people.NameAsync(i.ManagerUserId, ct));

        await fields.ApplyAsync(Docs.Project, p, i.CustomFields, isCreate: false, ct);
        await projects.SaveChangesAsync(ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<Unit> Handle(DeleteProject cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        if (await timesheets.AnyAsync(x => x.ProjectId == p.Id && x.IsBilled, ct) || await milestones.AnyAsync(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Invoiced, ct))
            throw new ConflictException("A project with invoiced work cannot be deleted. Complete or cancel it instead.");
        projects.Remove(p);
        await projects.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public async Task<ProjectDto> Handle(ChangeProjectStatus cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        ProjectsTransitions.Project.Ensure(p.Status, cmd.Status, isSystem: false);
        if (cmd.Status == ProjectStatus.Completed) await EnsureCanCompleteAsync(p, ct);
        p.Status = cmd.Status;
        await projects.SaveChangesAsync(ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<ProjectKpiDto> Handle(GetProjectKpiSummary q, CancellationToken ct)
    {
        // totals are added up in the database; templates and archived projects are left out
        var live = projects.Query(new Specification<Project>(x => !x.IsTemplate && !x.IsArchived));
        var totalProjects = await live.CountAsync(ct);
        var activeProjects = await live.CountAsync(x => x.Status == ProjectStatus.Active, ct);
        var completedProjects = await live.CountAsync(x => x.Status == ProjectStatus.Completed, ct);
        var totalBudget = await live.SumDecimalAsync(x => x.BudgetAmount, ct);
        var totalHours = await timesheets.Query().SumDecimalAsync(x => x.Hours, ct);
        var unbilledHoursAmount = await timesheets.Query(new Specification<ProjectTimesheet>(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved)).SumDecimalAsync(x => x.TotalAmount, ct);
        var unbilledMilestoneAmount = await milestones.Query(new Specification<ProjectMilestone>(x => x.Status == MilestoneStatus.Achieved)).SumDecimalAsync(x => x.Amount, ct);
        return new ProjectKpiDto(totalProjects, activeProjects, completedProjects, totalBudget, totalHours, unbilledHoursAmount + unbilledMilestoneAmount);
    }

    /// <summary>Checks the customer and returns its name; the project always shows the name on the customer record.</summary>
    private async Task<string?> EnsureCustomerAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id) return null;
        var party = await parties.FindAsync(id, ct) ?? throw new FieldValidationException("customerId", "Customer not found.");
        if (party.Roles != ERP.Domain.Parties.PartyRoles.None && !party.Roles.HasFlag(ERP.Domain.Parties.PartyRoles.Customer))
            throw new FieldValidationException("customerId", "Choose a customer.");
        return party.Name;
    }

    /// <summary>A project is completed only when nothing is left open or unbilled.</summary>
    private async Task EnsureCanCompleteAsync(Project p, CancellationToken ct)
    {
        var id = p.Id;
        var open = await tasks.Query(new Specification<ProjectTask>(t => t.ProjectId == id && t.Status != ProjectTaskStatus.Done && t.Status != ProjectTaskStatus.Cancelled)).CountAsync(ct);
        if (open > 0) throw new ConflictException($"{open} task(s) are still open. Finish or cancel them before completing the project.");
        if (p.BillingType == ProjectBillingType.NonBillable) return;
        var unbilledTime = await timesheets.Query(new Specification<ProjectTimesheet>(t => t.ProjectId == id && t.IsBillable && !t.IsBilled && !t.WrittenOff && t.ApprovalStatus == TimesheetStatus.Approved)).CountAsync(ct);
        var unbilledCosts = await expenses.Query(new Specification<ProjectExpense>(e => e.ProjectId == id && e.IsBillable && !e.IsBilled && !e.WrittenOff && e.Status == ExpenseStatus.Approved)).CountAsync(ct);
        var unbilledMilestones = await milestones.Query(new Specification<ProjectMilestone>(m => m.ProjectId == id && m.Status == MilestoneStatus.Achieved)).CountAsync(ct);
        if (unbilledTime + unbilledCosts + unbilledMilestones > 0)
            throw new ConflictException($"Bill or write off the remaining work first ({unbilledTime} time entries, {unbilledCosts} expenses, {unbilledMilestones} milestones).");
    }

    private static void Map(ProjectInput i, Project p, string? customerName, string? managerName)
    {
        p.Name = i.Name.Trim();
        p.Description = i.Description?.Trim();
        p.CustomerId = i.CustomerId;
        p.CustomerName = i.CustomerId is null ? i.CustomerName?.Trim() : customerName;
        p.ManagerName = managerName ?? i.ManagerName?.Trim();
        p.ManagerUserId = i.ManagerUserId;
        p.TaxCodeId = i.TaxCodeId;
        p.BudgetHours = i.BudgetHours;
        p.SourceType = string.IsNullOrWhiteSpace(i.SourceType) ? null : i.SourceType.Trim();
        p.SourceId = i.SourceId;
        p.IsTemplate = i.IsTemplate;
        p.Status = i.Status;
        p.BillingType = i.BillingType;
        p.BudgetAmount = i.BudgetAmount;
        p.StartDate = i.StartDate;
        p.EndDate = i.EndDate;
    }

    private async Task<ProjectDto> BuildDtoAsync(Project p, CancellationToken ct)
    {
        var pTimesheets = await timesheets.Query(new Specification<ProjectTimesheet>(x => x.ProjectId == p.Id)).ToListAsync(ct);
        var pTasks = await tasks.Query(new Specification<ProjectTask>(x => x.ProjectId == p.Id)).ToListAsync(ct);
        var pMilestones = await milestones.Query(new Specification<ProjectMilestone>(x => x.ProjectId == p.Id)).ToListAsync(ct);
        var pExpenses = await expenses.Query(new Specification<ProjectExpense>(x => x.ProjectId == p.Id)).ToListAsync(ct);
        var pChanges = await changeOrders.Query(new Specification<ProjectChangeOrder>(x => x.ProjectId == p.Id)).ToListAsync(ct);
        var pProgress = await progress.Query(new Specification<ProjectProgressBilling>(x => x.ProjectId == p.Id && !x.Released)).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalLogged = pTimesheets.Sum(x => x.Hours);
        var totalBilled = pTimesheets.Where(x => x.IsBilled).Sum(x => x.Hours);
        var labourCost = pTimesheets.Sum(x => x.CostAmount);
        var billableValue = pTimesheets.Where(x => x.IsBillable && !x.WrittenOff).Sum(x => x.TotalAmount);
        var expenseCost = pExpenses.Where(x => x.Status == ExpenseStatus.Approved).Sum(x => x.Amount);
        decimal Charge(ProjectExpense e) => ProjectMath.Round(e.Amount * (1m + e.MarkupPercent / 100m));
        var billed = pTimesheets.Where(x => x.IsBilled).Sum(x => x.TotalAmount) + pExpenses.Where(x => x.IsBilled).Sum(Charge)
            + pMilestones.Where(x => x.Status == MilestoneStatus.Invoiced).Sum(x => x.Amount) + pProgress.Sum(x => x.Amount);
        var unbilled = p.BillingType == ProjectBillingType.NonBillable ? 0m
            : (p.BillingType == ProjectBillingType.FixedFee ? 0m : pTimesheets.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.ApprovalStatus == TimesheetStatus.Approved).Sum(x => x.TotalAmount))
              + pExpenses.Where(x => x.IsBillable && !x.IsBilled && !x.WrittenOff && x.Status == ExpenseStatus.Approved).Sum(Charge)
              + pMilestones.Where(x => x.Status == MilestoneStatus.Achieved).Sum(x => x.Amount);
        var budget = ProjectMath.EffectiveBudget(p.BudgetAmount, pChanges);
        var approvedChanges = budget - p.BudgetAmount;
        var open = pTasks.Count(x => x.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled));
        var overdue = pTasks.Count(x => x.DueDate is { } d && d < today && x.Status is not (ProjectTaskStatus.Done or ProjectTaskStatus.Cancelled));
        var customJson = await fields.ReadAsync(Docs.Project, p, ct);

        return new ProjectDto(
            p.Id, p.Number, p.Name, p.Description, p.LegalEntityId, p.CustomerId, p.CustomerName, p.ManagerName, p.Status, p.BillingType, p.BudgetAmount, p.StartDate, p.EndDate,
            totalLogged, totalBilled, labourCost + expenseCost, pTasks.Count, pTasks.Count(x => x.Status == ProjectTaskStatus.Done),
            pMilestones.Count, pMilestones.Count(x => x.Status == MilestoneStatus.Achieved || x.Status == MilestoneStatus.Invoiced),
            customJson, p.CreatedAt,
            p.ManagerUserId, p.TaxCodeId, p.BudgetHours, p.IsTemplate, p.IsArchived, p.SourceType, p.SourceId,
            budget, approvedChanges, billableValue, expenseCost, billed, unbilled, ProjectMath.PercentComplete(pTasks),
            ProjectMath.Health(p, budget, labourCost + expenseCost, overdue, open, today), overdue);
    }
}
