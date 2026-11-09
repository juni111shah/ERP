using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Deletes a draft change order.</summary>
public sealed record DeleteChangeOrderCommand(Guid Id) : ICommand, IRequirePermission
{
    public string RequiredPermission => P.ChangeOrdersManage;
}
