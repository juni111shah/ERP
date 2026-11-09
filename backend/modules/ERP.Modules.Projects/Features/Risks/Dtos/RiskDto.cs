using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record RiskInput(Guid ProjectId, RiskKind Kind, string Title, string? Notes, RiskSeverity Severity, RiskStatus Status, Guid? OwnerUserId = null, string? OwnerName = null, DateOnly? DueDate = null);

public sealed record RiskDto(Guid Id, Guid ProjectId, RiskKind Kind, string Title, string? Notes, RiskSeverity Severity, RiskStatus Status, Guid? OwnerUserId, string? OwnerName, DateOnly? DueDate, DateTime CreatedAt)
{
    public static RiskDto From(ProjectRisk r) => new(r.Id, r.ProjectId, r.Kind, r.Title, r.Notes, r.Severity, r.Status, r.OwnerUserId, r.OwnerName, r.DueDate, r.CreatedAt);
}
