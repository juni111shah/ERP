using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Application.Platform;

/// <summary>Asks the platform to move the caller's workspace to another public plan. Returns the updated billing page.</summary>
public sealed record RequestPlanChangeCommand(PlanChangeInput Input) : ICommand<BillingOverview>, IRequirePermission
{
    public string RequiredPermission => CorePermissions.BillingManage;
}
