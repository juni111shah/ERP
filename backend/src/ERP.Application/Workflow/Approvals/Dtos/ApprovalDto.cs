using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

public sealed record ApprovalInput(string Subject, string? Justification, decimal? Amount, Guid? LegalEntityId);

public sealed record ApprovalStepDto(Guid Id, int Level, string ApproverPermission, ApprovalStepStatus Status, Guid? DecidedBy, DateTime? DecidedAt, string? Comment);

/// <summary><c>CurrentLevel</c> and <c>CurrentApproverPermission</c> are null once decided. <c>Steps</c> is the full decision history, lowest level first (empty on requests from before chains existed).</summary>
public sealed record ApprovalDto(
    Guid Id, Guid? LegalEntityId, string DocumentType, Guid? DocumentId, string Subject, string? Justification, decimal? Amount, string? CurrencyCode,
    Guid RequestedBy, DateTime RequestedAt, ApprovalStatus Status, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionComment,
    int? CurrentLevel = null, string? CurrentApproverPermission = null, IReadOnlyList<ApprovalStepDto>? Steps = null)
{
    public static ApprovalDto From(ApprovalRequest r)
    {
        var current = r.CurrentStep;
        var open = r.Status == ApprovalStatus.Pending;
        return new(
            r.Id, r.LegalEntityId, r.DocumentType, r.DocumentId, r.Subject, r.Justification, r.Amount, r.CurrencyCode,
            r.RequestedBy, r.RequestedAt, r.Status, r.DecidedBy, r.DecidedAt, r.DecisionComment,
            open ? current?.Level ?? 1 : null, open ? ApprovalRouting.EffectivePermission(current?.ApproverPermission) : null,
            r.Steps.OrderBy(s => s.Level).Select(s => new ApprovalStepDto(s.Id, s.Level, s.ApproverPermission, s.Status, s.DecidedBy, s.DecidedAt, s.Comment)).ToList());
    }
}
