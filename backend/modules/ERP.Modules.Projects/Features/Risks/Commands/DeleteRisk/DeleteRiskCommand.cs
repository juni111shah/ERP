using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a risk or issue.</summary>
public sealed record DeleteRiskCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.RisksManage;
}
