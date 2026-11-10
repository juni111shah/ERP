using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Workflow;

/// <summary>Deletes an approval policy.</summary>
public sealed record DeleteApprovalPolicyCommand(Guid Id) : ICommand, IRequirePermission { public string RequiredPermission => CorePermissions.SettingsWrite; }
