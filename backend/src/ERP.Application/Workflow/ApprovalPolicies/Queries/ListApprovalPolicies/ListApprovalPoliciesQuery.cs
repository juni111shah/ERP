using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>Rules for when a document needs a second person's approval before it is posted (for example bills of 5,000 or more).</summary>
public sealed record ListApprovalPoliciesQuery : IQuery<IReadOnlyList<ApprovalPolicyDto>>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsRead; }
