using ERP.Application.Customization;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

/// <summary>Builds the project detail view: the entity, its figures and its custom fields. Shared by the project handlers.</summary>
public sealed class ProjectDtoBuilder(ProjectFigures figures, ICustomFieldService fields)
{
    public async Task<ProjectDto> BuildAsync(Project p, CancellationToken ct)
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
