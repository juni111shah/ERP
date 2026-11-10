using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Withdraws a pending request (the approval service checks it is the caller's own).</summary>
public sealed record WithdrawApprovalCommand(Guid Id) : ICommand<ApprovalDto>;
