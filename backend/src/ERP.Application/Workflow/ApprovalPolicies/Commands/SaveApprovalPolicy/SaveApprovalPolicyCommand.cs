using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>Creates an approval policy (<c>Id</c> null) or updates one.</summary>
public sealed record SaveApprovalPolicyCommand(Guid? Id, ApprovalPolicyInput Input) : ICommand<ApprovalPolicyDto>, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
