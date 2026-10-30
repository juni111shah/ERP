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
    JsonObject? CustomFields = null);

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
    DateTime CreatedAt);

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
    DateTime CreatedAt);

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
        return await BuildDtoAsync(p, ct);
    }

    public async Task<ProjectDto> Handle(CreateProject cmd, CancellationToken ct)
    {
        var i = cmd.Input;
        var entity = await guard.RequireAsync(i.LegalEntityId, ct);
        if (i.Status is not (ProjectStatus.Planning or ProjectStatus.Active))
            throw new FieldValidationException("status", "A new project starts as Planning or Active.");
        await EnsureCustomerAsync(i.CustomerId, ct);
        var p = new Project { LegalEntityId = entity.Id };
        Map(i, p);
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

        await EnsureCustomerAsync(i.CustomerId, ct);
        if (i.Status != p.Status) ProjectsTransitions.Project.Ensure(p.Status, i.Status, isSystem: false);
        var number = string.IsNullOrWhiteSpace(i.Number) ? p.Number : i.Number.Trim();
        if (number != p.Number && await projects.AnyAsync(x => x.Number == number && x.Id != p.Id, ct))
            throw new ConflictException($"Project number '{number}' is already in use.");

        p.LegalEntityId = i.LegalEntityId;
        p.Number = number;
        Map(i, p);

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
        p.Status = cmd.Status;
        await projects.SaveChangesAsync(ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<ProjectKpiDto> Handle(GetProjectKpiSummary q, CancellationToken ct)
    {
        var allProjects = await projects.Query().ToListAsync(ct);
        var totalProjects = allProjects.Count;
        var activeProjects = allProjects.Count(x => x.Status == ProjectStatus.Active);
        var completedProjects = allProjects.Count(x => x.Status == ProjectStatus.Completed);
        var totalBudget = allProjects.Sum(x => x.BudgetAmount);

        var ts = await timesheets.Query().ToListAsync(ct);
        var totalHours = ts.Sum(x => x.Hours);
        var unbilledHoursAmount = ts.Where(x => x.IsBillable && !x.IsBilled).Sum(x => x.TotalAmount);

        var ms = await milestones.Query().ToListAsync(ct);
        var unbilledMilestoneAmount = ms.Where(x => x.Status == MilestoneStatus.Achieved).Sum(x => x.Amount);

        return new ProjectKpiDto(
            totalProjects,
            activeProjects,
            completedProjects,
            totalBudget,
            totalHours,
            unbilledHoursAmount + unbilledMilestoneAmount);
    }

    private async Task EnsureCustomerAsync(Guid? customerId, CancellationToken ct)
    {
        if (customerId is not { } id) return;
        var party = await parties.FindAsync(id, ct) ?? throw new FieldValidationException("customerId", "Customer not found.");
        if (party.Roles != ERP.Domain.Parties.PartyRoles.None && !party.Roles.HasFlag(ERP.Domain.Parties.PartyRoles.Customer))
            throw new FieldValidationException("customerId", "Choose a customer.");
    }

    private static void Map(ProjectInput i, Project p)
    {
        p.Name = i.Name.Trim();
        p.Description = i.Description?.Trim();
        p.CustomerId = i.CustomerId;
        p.CustomerName = i.CustomerName?.Trim();
        p.ManagerName = i.ManagerName?.Trim();
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

        var totalLogged = pTimesheets.Sum(x => x.Hours);
        var totalBilled = pTimesheets.Where(x => x.IsBilled).Sum(x => x.Hours);
        var totalCost = pTimesheets.Sum(x => x.TotalAmount);

        var totalTasks = pTasks.Count;
        var completedTasks = pTasks.Count(x => x.Status == ProjectTaskStatus.Done);

        var totalMilestones = pMilestones.Count;
        var achievedMilestones = pMilestones.Count(x => x.Status == MilestoneStatus.Achieved || x.Status == MilestoneStatus.Invoiced);

        var customJson = await fields.ReadAsync(Docs.Project, p, ct);

        return new ProjectDto(
            p.Id,
            p.Number,
            p.Name,
            p.Description,
            p.LegalEntityId,
            p.CustomerId,
            p.CustomerName,
            p.ManagerName,
            p.Status,
            p.BillingType,
            p.BudgetAmount,
            p.StartDate,
            p.EndDate,
            totalLogged,
            totalBilled,
            totalCost,
            totalTasks,
            completedTasks,
            totalMilestones,
            achievedMilestones,
            customJson,
            p.CreatedAt);
    }
}
