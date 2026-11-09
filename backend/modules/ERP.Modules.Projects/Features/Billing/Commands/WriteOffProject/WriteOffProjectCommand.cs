using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Decides not to charge for all the approved, unbilled time and expenses of a project.</summary>
public sealed record WriteOffProjectCommand(Guid ProjectId) : ICommand<int>, IRequirePermission
{
    public string RequiredPermission => P.BillingManage;
}
