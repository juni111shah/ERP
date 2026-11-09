using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Platform;

/// <summary>Withdraws the workspace's pending plan-change request. Returns the updated billing page.</summary>
public sealed record WithdrawPlanChangeCommand : ICommand<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingManage;
}
