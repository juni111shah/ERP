using ERP.Application.Common;
using ERP.Application.Security;

namespace ERP.Modules.Projects.Features;

/// <summary>Edits a draft change order.</summary>
public sealed record UpdateChangeOrderCommand(Guid Id, ChangeOrderInput Input) : ICommand<ChangeOrderDto>, IRequirePermission
{
    public string RequiredPermission => P.ChangeOrdersManage;
}
