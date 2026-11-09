using System.Linq.Expressions;
using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record MilestoneInput(
    Guid ProjectId,
    string Title,
    DateOnly DueDate,
    decimal Amount,
    MilestoneStatus Status = MilestoneStatus.Pending,
    DateOnly? AchievedDate = null);

public sealed record MilestoneDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string Title,
    DateOnly DueDate,
    decimal Amount,
    MilestoneStatus Status,
    DateOnly? AchievedDate,
    DateTime CreatedAt)
{
    /// <summary>The list projection (translated to SQL); the project name is filled in afterwards by <see cref="ProjectListEnrich.Milestones"/>.</summary>
    public static readonly Expression<Func<ProjectMilestone, MilestoneDto>> Projection = m => new MilestoneDto(
        m.Id, m.ProjectId, "", m.Title, m.DueDate, m.Amount, m.Status, m.AchievedDate, m.CreatedAt);
}
