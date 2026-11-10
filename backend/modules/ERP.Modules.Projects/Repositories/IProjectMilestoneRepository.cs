using ERP.Application.Abstractions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Repositories;

/// <summary>Data access for project milestones. Implemented in ProjectMilestoneRepository.cs.</summary>
public interface IProjectMilestoneRepository : IRepository<ProjectMilestone>
{
    /// <summary>How many milestones of the project are achieved (ready to bill).</summary>
    Task<int> CountAchievedAsync(Guid projectId, CancellationToken ct);

    /// <summary>The title of a milestone; null when not found.</summary>
    Task<string?> GetTitleAsync(Guid id, CancellationToken ct);

    /// <summary>The amount of the project's milestones that are not cancelled, leaving out <paramref name="exceptId"/>.</summary>
    Task<decimal> SumLiveAmountAsync(Guid projectId, Guid? exceptId, CancellationToken ct);

    /// <summary>The amount of the project's invoiced milestones.</summary>
    Task<decimal> SumInvoicedAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's achieved milestones, by due date, read-only.</summary>
    Task<List<ProjectMilestone>> ListAchievedAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's achieved milestones (only <paramref name="onlyIds"/> when set), tracked for changes.</summary>
    Task<List<ProjectMilestone>> ListChosenAchievedForUpdateAsync(Guid projectId, IReadOnlyList<Guid>? onlyIds, CancellationToken ct);

    /// <summary>Project and amount of every achieved milestone.</summary>
    Task<List<ProjectAmount>> ListAchievedAmountsAsync(CancellationToken ct);

    /// <summary>Project and amount of every invoiced milestone.</summary>
    Task<List<ProjectAmount>> ListInvoicedAmountsAsync(CancellationToken ct);

    /// <summary>Every milestone of the project, read-only.</summary>
    Task<List<ProjectMilestone>> ListOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The project's milestones that are not cancelled, read-only.</summary>
    Task<List<ProjectMilestone>> ListLiveOfProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>The milestones of the projects that are not cancelled, read-only.</summary>
    Task<List<ProjectMilestone>> ListLiveOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The achieved and invoiced milestones of the projects, read-only.</summary>
    Task<List<ProjectMilestone>> ListAchievedOrInvoicedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The invoiced milestones of the projects, read-only.</summary>
    Task<List<ProjectMilestone>> ListInvoicedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);

    /// <summary>The achieved milestones of the projects, read-only.</summary>
    Task<List<ProjectMilestone>> ListAchievedOfProjectsAsync(List<Guid> projectIds, CancellationToken ct);
}

/// <summary>An amount of one project.</summary>
public sealed record ProjectAmount(Guid ProjectId, decimal Amount);
