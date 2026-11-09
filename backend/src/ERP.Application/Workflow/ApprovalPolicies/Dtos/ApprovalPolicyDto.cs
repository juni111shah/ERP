using ERP.Domain.Platform;

namespace ERP.Application.Workflow;

/// <summary><c>ApproverPermission</c> defaults to the core approver permission, <c>Level</c> to 1. Rows with higher levels for the same document type form a sequential chain.</summary>
public sealed record ApprovalPolicyInput(string DocumentType, decimal? MinAmount, bool IsActive = true, string? ApproverPermission = null, int? Level = null);

public sealed record ApprovalPolicyDto(Guid Id, string DocumentType, decimal? MinAmount, string ApproverPermission, bool IsActive, int Level = 1)
{
    public static ApprovalPolicyDto From(ApprovalPolicy p) => new(p.Id, p.DocumentType, p.MinAmount, ApprovalRouting.EffectivePermission(p.ApproverPermission), p.IsActive, p.Level);
}
