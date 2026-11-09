using System.Linq.Expressions;
using System.Text.Json.Nodes;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

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
    decimal? ContractValue = null)
{
    /// <summary>The list projection (translated to SQL); the figures are filled in afterwards by <see cref="ProjectListEnrich.Projects"/>.</summary>
    public static readonly Expression<Func<Project, ProjectSummaryDto>> Projection = p => new ProjectSummaryDto(
        p.Id, p.Number, p.Name, p.LegalEntityId, p.CustomerId, p.CustomerName, p.ManagerName, p.Status, p.BillingType, p.BudgetAmount, p.StartDate, p.EndDate, p.CreatedAt, p.ManagerUserId, p.IsTemplate, p.IsArchived, 0m, null, 0m, 0m, 0, 0, 0m, p.ContractValue);
}

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
