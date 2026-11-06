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
    decimal? BudgetHours = null,
    string? SourceType = null,
    Guid? SourceId = null,
    bool? IsTemplate = null,
    decimal? ContractValue = null);

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
    bool IsArchived = false,
    decimal PercentComplete = 0m,
    string? Health = null,
    decimal Cost = 0m,
    decimal EffectiveBudget = 0m,
    int OpenTasks = 0,
    int OverdueTasks = 0,
    decimal UnbilledAmount = 0m,
    decimal? ContractValue = null);

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
    int OverdueTasks,
    int OpenTasks = 0,
    decimal EffectiveBudgetHours = 0m,
    decimal LabourCost = 0m,
    IReadOnlyList<ProjectStatus>? NextStatuses = null,
    decimal? ContractValue = null,
    decimal WipBalance = 0m);

public sealed record ProjectKpiDto(
    int TotalProjects,
    int ActiveProjects,
    int CompletedProjects,
    decimal TotalBudget,
    decimal TotalLoggedHours,
    decimal UnbilledAmount,
    int OnHoldProjects = 0,
    int AtRiskProjects = 0,
    int OffTrackProjects = 0,
    int OverdueTasks = 0,
    decimal TotalCost = 0m,
    int TemplateCount = 0,
    int ArchivedCount = 0);

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
        RuleFor(x => x.ContractValue).GreaterThanOrEqualTo(0).When(x => x.ContractValue != null);
        RuleFor(x => x.BudgetHours).GreaterThanOrEqualTo(0).When(x => x.BudgetHours != null);
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
    IRepository<ProjectTimesheet> timesheets,
    IReadRepository<ERP.Domain.Parties.Party> parties,
    IRepository<ProjectExpense> expenses,
    IReadRepository<ProjectProgressBilling> progress,
    ProjectFigures figures,
    ProjectPeople people,
    ProjectAccess access,
    ILegalEntityGuard guard,
    INumberingService numbers,
    ICustomFieldService fields,
    ERP.Application.Finance.IPostingService posting,
    IUnitOfWork uow) :
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
        var p = new Project { LegalEntityId = entity.Id, TaxCodeId = i.TaxCodeId };
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

    public Task<ProjectDto> Handle(UpdateProject cmd, CancellationToken ct) => uow.InTransactionAsync(token => UpdateCoreAsync(cmd, token), ct);

    private async Task<ProjectDto> UpdateCoreAsync(UpdateProject cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureAsync(p, ct);
        var i = cmd.Input;
        if (i.LegalEntityId != p.LegalEntityId)
            await guard.RequireAsync(i.LegalEntityId, ct);

        var customerName = await EnsureCustomerAsync(i.CustomerId, ct);
        if (i.Status != p.Status) { ProjectsTransitions.Project.Ensure(p.Status, i.Status, isSystem: false); if (i.Status == ProjectStatus.Completed) await EnsureCanCompleteAsync(p, ct); }
        if (i.IsTemplate == true && !p.IsTemplate && await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct)) throw new ConflictException("A project that has time logged cannot become a template.");
        var number = string.IsNullOrWhiteSpace(i.Number) ? p.Number : i.Number.Trim();
        if (number != p.Number && await projects.AnyAsync(x => x.Number == number && x.Id != p.Id, ct))
            throw new ConflictException($"Project number '{number}' is already in use.");

        if (i.LegalEntityId != p.LegalEntityId && (await timesheets.AnyAsync(x => x.ProjectId == p.Id, ct) || await tasks.AnyAsync(x => x.ProjectId == p.Id, ct)))
            throw new ConflictException("A project that already has tasks or time cannot move to another legal entity.");
        p.LegalEntityId = i.LegalEntityId;
        p.Number = number;
        var closing = i.Status != p.Status && i.Status is ProjectStatus.Completed or ProjectStatus.Cancelled;
        Map(i, p, customerName, await people.NameAsync(i.ManagerUserId, ct));
        if (closing) await ProjectWip.ReleaseRemainingAsync(posting, timesheets, expenses, p, i.Status, ct);

        await fields.ApplyAsync(Docs.Project, p, i.CustomFields, isCreate: false, ct);
        await projects.SaveChangesAsync(ct);
        return await BuildDtoAsync(p, ct);
    }

    public async Task<Unit> Handle(DeleteProject cmd, CancellationToken ct)
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, ct);
        await access.EnsureAsync(p, ct);
        if (await timesheets.AnyAsync(x => x.ProjectId == p.Id && x.IsBilled, ct) || await expenses.AnyAsync(x => x.ProjectId == p.Id && x.IsBilled, ct)
            || await milestones.AnyAsync(x => x.ProjectId == p.Id && x.Status == MilestoneStatus.Invoiced, ct) || await progress.AnyAsync(x => x.ProjectId == p.Id && !x.Released, ct))
            throw new ConflictException("A project with invoiced work cannot be deleted. Complete or cancel it instead.");
        if (await timesheets.AnyAsync(x => x.ProjectId == p.Id && x.CostJournalId != null, ct) || await expenses.AnyAsync(x => x.ProjectId == p.Id && x.CostJournalId != null, ct))
            throw new ConflictException("The cost of this project is already in the ledger, so it cannot be deleted. Complete or cancel it instead.");
        projects.Remove(p);
        await projects.SaveChangesAsync(ct);
        return Unit.Value;
    }

    public Task<ProjectDto> Handle(ChangeProjectStatus cmd, CancellationToken ct) => uow.InTransactionAsync(async token =>
    {
        var p = await projects.GetForUpdateAsync(cmd.Id, token);
        await access.EnsureAsync(p, token);
        ProjectsTransitions.Project.Ensure(p.Status, cmd.Status, isSystem: false);
        if (cmd.Status == ProjectStatus.Completed) await EnsureCanCompleteAsync(p, token);
        p.Status = cmd.Status;
        if (cmd.Status is ProjectStatus.Completed or ProjectStatus.Cancelled) await ProjectWip.ReleaseRemainingAsync(posting, timesheets, expenses, p, cmd.Status, token);
        await projects.SaveChangesAsync(token);
        return await BuildDtoAsync(p, token);
    }, ct);

    public async Task<ProjectKpiDto> Handle(GetProjectKpiSummary q, CancellationToken ct)
    {
        // templates and archived projects are left out; the figures are the ones the billing page shows
        var visible = await access.VisibleIdsAsync(ct);
        var all = (await projects.Query().ToListAsync(ct)).Where(x => visible == null || visible.Contains(x.Id)).ToList();
        var live = all.Where(x => !x.IsTemplate && !x.IsArchived).ToList();
        var rows = await figures.ForAsync(live, ct);
        return new ProjectKpiDto(
            live.Count,
            live.Count(x => x.Status == ProjectStatus.Active),
            live.Count(x => x.Status == ProjectStatus.Completed),
            rows.Values.Sum(x => x.Budget),
            rows.Values.Sum(x => x.LoggedHours),
            rows.Values.Sum(x => x.Unbilled),
            live.Count(x => x.Status == ProjectStatus.OnHold),
            rows.Values.Count(x => x.Health == "atRisk"),
            rows.Values.Count(x => x.Health == "offTrack"),
            rows.Values.Sum(x => x.OverdueTasks),
            rows.Values.Sum(x => x.Cost),
            all.Count(x => x.IsTemplate),
            all.Count(x => x.IsArchived && !x.IsTemplate));
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
        // a fixed-fee project bills milestones and percentages, so its hours never wait to be billed
        var unbilledTime = p.BillingType == ProjectBillingType.FixedFee ? 0 : await timesheets.Query(new Specification<ProjectTimesheet>(t => t.ProjectId == id && t.IsBillable && !t.IsBilled && !t.WrittenOff && t.ApprovalStatus == TimesheetStatus.Approved)).CountAsync(ct);
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
        // What a form does not send stays as it was: a screen or an older app that only knows some fields must not wipe the others.
        var typedManager = i.ManagerName?.Trim();
        p.ManagerUserId = i.ManagerUserId ?? (string.Equals(typedManager, p.ManagerName, StringComparison.Ordinal) ? p.ManagerUserId : null);
        p.ManagerName = managerName ?? typedManager;
        p.TaxCodeId = i.TaxCodeId == Guid.Empty ? null : i.TaxCodeId ?? p.TaxCodeId;   // an empty id clears it
        p.BudgetHours = i.BudgetHours ?? p.BudgetHours;
        if (!string.IsNullOrWhiteSpace(i.SourceType)) p.SourceType = i.SourceType.Trim();
        p.SourceId = i.SourceId ?? p.SourceId;
        p.IsTemplate = i.IsTemplate ?? p.IsTemplate;
        p.Status = i.Status;
        p.BillingType = i.BillingType;
        p.BudgetAmount = i.BudgetAmount;
        p.ContractValue = i.ContractValue ?? p.ContractValue;
        p.StartDate = i.StartDate;
        p.EndDate = i.EndDate;
    }

    private async Task<ProjectDto> BuildDtoAsync(Project p, CancellationToken ct)
    {
        var f = await figures.ForAsync(p, ct);
        var customJson = await fields.ReadAsync(Docs.Project, p, ct);
        return new ProjectDto(
            p.Id, p.Number, p.Name, p.Description, p.LegalEntityId, p.CustomerId, p.CustomerName, p.ManagerName, p.Status, p.BillingType, p.BudgetAmount, p.StartDate, p.EndDate,
            f.LoggedHours, f.BilledHours, f.Cost, f.TotalTasks, f.CompletedTasks, f.TotalMilestones, f.AchievedMilestones, customJson, p.CreatedAt,
            p.ManagerUserId, p.TaxCodeId, p.BudgetHours, p.IsTemplate, p.IsArchived, p.SourceType, p.SourceId,
            f.Budget, f.ApprovedChanges, f.BillableValue, f.ExpenseCost, f.Billed, f.Unbilled, f.PercentComplete, f.Health, f.OverdueTasks, f.OpenTasks, f.BudgetHours, f.LabourCost,
            ProjectsTransitions.ProjectNext(p.Status), p.ContractValue, f.WipBalance);
    }
}
