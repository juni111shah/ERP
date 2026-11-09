using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record PortalMilestoneDto(Guid Id, string Title, DateOnly DueDate, MilestoneStatus Status, DateOnly? AchievedDate);
public sealed record PortalProjectDto(Guid Id, string Number, string Name, ProjectStatus Status, DateOnly? StartDate, DateOnly? EndDate, decimal PercentComplete, string? ManagerName, IReadOnlyList<PortalMilestoneDto> Milestones);

public sealed record PortalAcceptanceDto(Guid Id, Guid ProjectId, string ProjectName, string Title, AcceptanceStatus Status, DateTime RequestedAt, string? SignedByName, DateTime? SignedAt)
{
    public static PortalAcceptanceDto From(ProjectAcceptance a, string project) => new(a.Id, a.ProjectId, project, a.Title, a.Status, a.RequestedAt, a.SignedByName, a.SignedAt);
}

public sealed record SignAcceptanceInput(bool Accept, string SignedByName, string? Signature, string? Comment, string? SignatureImage = null);
