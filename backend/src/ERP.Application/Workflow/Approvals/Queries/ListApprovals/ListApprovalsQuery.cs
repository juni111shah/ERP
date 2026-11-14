using ERP.Application.Security;
using ERP.Application.Common;

namespace ERP.Application.Workflow;

/// <summary>"mine" = requests I made; otherwise the pending requests whose current level I may decide, and for a user who may decide nothing their own.</summary>
public sealed record ListApprovalsQuery(string? Scope) : IQuery<IReadOnlyList<ApprovalDto>>, IAuthorizesInHandler;
