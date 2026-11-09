using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Raises a customer invoice for the chosen billable work of a project and marks that work as billed.</summary>
public sealed record BillProjectCommand(BillProjectInput Input) : ICommand<BillProjectResultDto>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}
