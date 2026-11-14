using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>Approves or rejects the current level. Its permission is checked by the approval service: it differs per policy.</summary>
public sealed record DecideApprovalCommand(Guid Id, bool Approve, string? Comment) : ICommand<ApprovalDto>, IAuthorizesInHandler;
