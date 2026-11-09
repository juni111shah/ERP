using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Logs (no id) or updates a risk or issue of a project.</summary>
public sealed record SaveRiskCommand(Guid? Id, RiskInput Input) : ICommand<RiskDto>, IRequirePermission
{
    public string RequiredPermission => P.RisksManage;
}
