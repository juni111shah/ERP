using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Submits a general approval request (for a company the caller may use).</summary>
public sealed record SubmitApprovalCommand(ApprovalInput Input) : ICommand<ApprovalDto>;
