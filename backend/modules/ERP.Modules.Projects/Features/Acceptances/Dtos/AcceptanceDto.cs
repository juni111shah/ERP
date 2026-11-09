using ERP.Modules.Projects.Domain;

namespace ERP.Modules.Projects.Features;

public sealed record RequestAcceptanceInput(Guid ProjectId, Guid? MilestoneId, string Title);
public sealed record AcceptanceDto(Guid Id, Guid ProjectId, string ProjectName, Guid? MilestoneId, string Title, AcceptanceStatus Status, DateTime RequestedAt, string? SignedByName, DateTime? SignedAt, string? Comment,
    bool DrawnSignature = false, string? SignedIp = null, string? EvidenceHash = null);
